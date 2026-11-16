namespace MauiApp.Models;

public enum CompressionLevel { Low, Medium, High }

public enum NumberPosition { BottomCenter, BottomRight, BottomLeft, TopCenter, TopRight, TopLeft }

public enum SignaturePosition { BottomRight, BottomLeft, BottomCenter, Center, TopRight, TopLeft, TopCenter }

public enum EncryptionStrength { Aes128, Aes256 }

public enum ScanFilter { Original, Enhanced, Grayscale, BlackAndWhite }

public sealed record PageNumberOptions(
    NumberPosition Position = NumberPosition.BottomCenter,
    int StartNumber = 1,
    string Format = "{n}",
    double FontSize = 12,
    double Margin = 28,
    bool SkipFirstPage = false,
    string PageRanges = "");

public sealed record WatermarkOptions(
    string Text = "CONFIDENTIAL",
    byte[]? ImageBytes = null,
    double Opacity = 0.25,
    double Rotation = 45,
    bool Tile = false,
    double FontSize = 64,
    double ImageWidthPercent = 40,
    string PageRanges = "");

public sealed record PdfPermissions(bool AllowPrint = true, bool AllowCopy = true, bool AllowEdit = true, bool AllowAnnotate = true);

public sealed record SignOptions(
    byte[] SignaturePng,
    int Page = 0,
    SignaturePosition Position = SignaturePosition.BottomRight,
    double WidthPercent = 30,
    string? SignerName = null,
    bool AddTimestamp = true,
    bool AllPages = false);
