using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;

namespace MauiApp.PageModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly FeatureService _features;

    public LoginViewModel(AuthService auth, FeatureService features)
    {
        _auth = auth;
        _features = features;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignIn), nameof(Title), nameof(Subtitle), nameof(SubmitText), nameof(SwitchText), nameof(SwitchAction))]
    private bool _isSignUp;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HidePassword))]
    private bool _showPassword;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = string.Empty;

    [ObservableProperty] private bool _isBusy;

    public bool IsSignIn => !IsSignUp;
    public bool HidePassword => !ShowPassword;
    public bool HasError => Error.Length > 0;
    public string Title => IsSignUp ? "Create your account" : "Welcome back";
    public string Subtitle => IsSignUp ? "Sign up to start using every PDF tool" : "Sign in to continue";
    public string SubmitText => IsSignUp ? "Create account" : "Sign in";
    public string SwitchText => IsSignUp ? "Already have an account?" : "New here?";
    public string SwitchAction => IsSignUp ? "Sign in" : "Create an account";

    [RelayCommand]
    private void ToggleMode()
    {
        IsSignUp = !IsSignUp;
        Error = string.Empty;
        Password = ConfirmPassword = string.Empty;
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (IsBusy)
            return;
        Error = string.Empty;

        if (IsSignUp && !_features.Global.AllowSignUp)
        {
            Error = "New registrations are currently closed by the administrator.";
            return;
        }

        IsBusy = true;
        try
        {
            // PBKDF2 is intentionally slow, so keep it off the UI thread.
            var name = Name;
            var email = Email;
            var password = Password;
            var confirm = ConfirmPassword;
            var signUp = IsSignUp;
            var result = await Task.Run(() => signUp
                ? _auth.SignUp(name, email, password, confirm)
                : _auth.SignIn(email, password));

            if (!result.Success)
            {
                Error = result.Error ?? "Something went wrong.";
                return;
            }

            Password = ConfirmPassword = string.Empty;
            if (Application.Current?.Windows.FirstOrDefault() is { } window)
                window.Page = new AppShell();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
