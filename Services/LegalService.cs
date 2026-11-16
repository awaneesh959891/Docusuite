namespace MauiApp.Services;

public static class LegalService
{
    public const string CurrentVersion = "1.0";
    private const string AcceptedVersionKey = "legal_accepted_version";

    public static bool IsAccepted => Preferences.Default.Get(AcceptedVersionKey, string.Empty) == CurrentVersion;

    public static void Accept() => Preferences.Default.Set(AcceptedVersionKey, CurrentVersion);

    public const string Disclaimer =
        "DocSuite is provided as a document utility and does not provide legal, compliance, archival, identity-verification, or professional advice. " +
        "Always keep a backup before editing a document. Results can vary with malformed, encrypted, scanned, or unusually structured files. " +
        "A visual signature created by this app is an image stamp and is not an X.509 digital signature. Use unlock, redaction, signing, and conversion features only on documents you own or are authorized to process.";

    public const string PrivacySummary =
        "DocSuite is designed for local processing. Selected documents, generated files, local accounts, settings, and activity history remain on this device unless you explicitly save, share, or open them with another application. " +
        "Passwords are stored as salted hashes. Configured AI API keys are stored in the operating system secure storage. Admin controls and user accounts in this build are device-local and are not synchronized to a server.";

    public const string DataDetails =
        "Data handled on this device may include files you select or scan, generated PDFs, file names, tool usage history, local account email/display name, password hashes, app settings, and optional AI provider credentials. " +
        "Camera access is used only when you choose Scan to PDF. File access is used only for files you select or destinations you choose. DocSuite does not sell personal data and has no built-in analytics or advertising SDK. " +
        "If you configure a third-party AI provider in a future AI-enabled release, content submitted to that provider will be governed by the provider's privacy terms. Current AI settings do not send documents by themselves.";

    public const string Retention =
        "Generated files and history remain until you delete them from Admin, remove the app, or clear application data. Temporary files can be cleared automatically on launch or manually from Admin. " +
        "Sharing, saving, or opening a file creates a copy or grants access outside DocSuite; deletion inside DocSuite cannot remove copies held by other applications or cloud services.";

    public const string Terms =
        "You are responsible for confirming that you have permission to process each document and for verifying every output before relying on it. " +
        "Do not use the app to bypass access controls, infringe copyright, impersonate another person, or process unlawful content. " +
        "The software is provided without warranties to the maximum extent permitted by applicable law. The developer is not liable for lost files, lost profits, compliance failures, or indirect damages arising from use of the app. " +
        "These terms and the privacy notice may be updated when functionality or data handling changes.";
}
