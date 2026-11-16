using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiApp.Services;

public enum UserRole
{
    User,
    Admin
}

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }
    public bool Disabled { get; set; }

    [JsonIgnore] public bool IsAdmin => Role == UserRole.Admin;
    [JsonIgnore] public string Status => Disabled ? "Disabled" : "Active";
    [JsonIgnore] public string ToggleText => Disabled ? "Enable" : "Disable";
    [JsonIgnore] public string Joined => $"Joined {CreatedAt:MMM d, yyyy}";
    [JsonIgnore] public string LastSeen => LastLoginAt is { } t ? $"Last sign-in {t:MMM d, h:mm tt}" : "Never signed in";
}

public sealed record AuthResult(bool Success, string? Error = null);

// Local accounts for this device. Passwords are stored only as salted PBKDF2 hashes.
public class AuthService
{
    public const string AdminEmail = "admin@gmail.com";
    private const string AdminSeedPassword = "awaneesh00";
    private const string SessionKey = "session_user";
    private const int HashIterations = 120_000;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _file = Path.Combine(FileSystem.AppDataDirectory, "users.json");
    private List<UserAccount>? _users;
    private UserAccount? _current;

    public event Action? SessionChanged;
    public event Action? UsersChanged;

    public AuthService() => RestoreSession();

    public UserAccount? Current => _current;
    public bool IsSignedIn => _current is not null;
    public bool IsAdmin => _current?.IsAdmin == true;

    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidEmail(string email)
    {
        if (email.Length is < 5 or > 254 || email.Contains(' '))
            return false;
        var at = email.IndexOf('@');
        return at > 0 && at == email.LastIndexOf('@') && email.IndexOf('.', at) > at + 1 && !email.EndsWith('.');
    }

    public AuthResult SignUp(string? name, string? emailInput, string? password, string? confirm)
    {
        var email = Normalize(emailInput);
        name = (name ?? string.Empty).Trim();

        if (name.Length < 2)
            return new(false, "Please enter your name.");
        if (!IsValidEmail(email))
            return new(false, "Enter a valid email address.");
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            return new(false, "Password must be at least 6 characters.");
        if (password != confirm)
            return new(false, "The passwords don't match.");

        lock (_gate)
        {
            var users = Load();
            if (users.Any(u => u.Email == email))
                return new(false, "An account with this email already exists.");

            var account = new UserAccount { Email = email, DisplayName = name, Role = UserRole.User, LastLoginAt = DateTime.Now };
            SetPassword(account, password);
            users.Add(account);
            Save();
            StartSession(account);
        }
        UsersChanged?.Invoke();
        SessionChanged?.Invoke();
        return new(true);
    }

    public AuthResult SignIn(string? emailInput, string? password)
    {
        var email = Normalize(emailInput);
        if (email.Length == 0 || string.IsNullOrEmpty(password))
            return new(false, "Enter your email and password.");

        lock (_gate)
        {
            var account = Load().FirstOrDefault(u => u.Email == email);
            if (account is null || !Verify(account, password))
                return new(false, "Incorrect email or password.");
            if (account.Disabled)
                return new(false, "This account has been disabled by the administrator.");

            account.LastLoginAt = DateTime.Now;
            Save();
            StartSession(account);
        }
        UsersChanged?.Invoke();
        SessionChanged?.Invoke();
        return new(true);
    }

    public void SignOut()
    {
        _current = null;
        Preferences.Default.Remove(SessionKey);
        SessionChanged?.Invoke();
    }

    public IReadOnlyList<UserAccount> Users()
    {
        lock (_gate)
            return Load().OrderByDescending(u => u.IsAdmin).ThenBy(u => u.Email).ToList();
    }

    public AuthResult SetDisabled(UserAccount user, bool disabled)
    {
        lock (_gate)
        {
            var account = Load().FirstOrDefault(u => u.Id == user.Id);
            if (account is null)
                return new(false, "User not found.");
            if (account.IsAdmin)
                return new(false, "The administrator account can't be disabled.");
            account.Disabled = disabled;
            Save();
        }
        UsersChanged?.Invoke();
        return new(true);
    }

    public AuthResult Delete(UserAccount user)
    {
        lock (_gate)
        {
            var account = Load().FirstOrDefault(u => u.Id == user.Id);
            if (account is null)
                return new(false, "User not found.");
            if (account.IsAdmin)
                return new(false, "The administrator account can't be deleted.");
            Load().Remove(account);
            Save();
        }
        UsersChanged?.Invoke();
        return new(true);
    }

    private void StartSession(UserAccount account)
    {
        _current = account;
        Preferences.Default.Set(SessionKey, account.Id.ToString());
    }

    private void RestoreSession()
    {
        var id = Preferences.Default.Get(SessionKey, string.Empty);
        if (!Guid.TryParse(id, out var guid))
            return;
        lock (_gate)
        {
            var account = Load().FirstOrDefault(u => u.Id == guid);
            if (account is { Disabled: false })
                _current = account;
            else
                Preferences.Default.Remove(SessionKey);
        }
    }

    private List<UserAccount> Load()
    {
        if (_users is not null)
            return _users;
        try
        {
            _users = File.Exists(_file)
                ? JsonSerializer.Deserialize<List<UserAccount>>(File.ReadAllText(_file)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _users = [];
        }

        if (!_users.Any(u => u.Role == UserRole.Admin))
        {
            var admin = new UserAccount { Email = AdminEmail, DisplayName = "Administrator", Role = UserRole.Admin };
            SetPassword(admin, AdminSeedPassword);
            _users.Add(admin);
            Save();
        }
        return _users;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(_users, Json));
    }

    private static void SetPassword(UserAccount account, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        account.Salt = Convert.ToBase64String(salt);
        account.Iterations = HashIterations;
        account.PasswordHash = Convert.ToBase64String(
            Rfc2898DeriveBytes.Pbkdf2(password, salt, HashIterations, HashAlgorithmName.SHA256, 32));
    }

    private static bool Verify(UserAccount account, string password)
    {
        try
        {
            var salt = Convert.FromBase64String(account.Salt);
            var expected = Convert.FromBase64String(account.PasswordHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, account.Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
