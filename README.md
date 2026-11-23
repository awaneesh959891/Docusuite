# DocSuite

**View, edit, convert, organize and protect PDFs entirely on your device. No uploads. No internet required.**

DocSuite is an offline-first PDF management app built with **.NET MAUI** for Windows, Android and iOS (Mac Catalyst is also configured). Files stay on the device and no cloud account is needed to use the tools.

> Status: active development. Some roadmap items are not implemented yet; see [Roadmap](#roadmap).

## Table of contents

1. [Features](#features)
2. [Tech stack](#tech-stack)
3. [Prerequisites](#prerequisites)
4. [Getting started](#getting-started)
5. [Running the app](#running-the-app)
6. [Building release packages](#building-release-packages)
7. [Project structure](#project-structure)
8. [Configuration and local data](#configuration-and-local-data)
9. [Troubleshooting](#troubleshooting)
10. [Roadmap](#roadmap)
11. [Contributing](#contributing)

## Features

### Document workspace
- **My Documents**: local PDF workspace with recent files, favorites, pins, tags, folders, sorting, and grid/list views.
- File actions (rename, duplicate, move, share, delete) and multi-select.
- **Recycle bin** with restore and permanent delete.
- Cached local thumbnails.

### PDF tools
- Merge, split, compress, rotate, watermark, page numbers, protect (password) and unlock.
- Visual (electronic) signature. This is **not** a certificate-based digital signature.
- Image to PDF, PDF to PNG, and PDF to JPG (every page exported as an image).
- Visual page cropping and basic document metadata removal.
- Full-screen PDF preview. On Android, the native `PdfRenderer` is used, so preview works on all supported versions (API 24+). Other platforms use PDFtoImage.

**Important limitations:** Crop changes the PDF page crop box; it does not erase the underlying content and must not be used to conceal sensitive information. Basic metadata removal clears common Info fields and XMP metadata only; it is not a full document sanitizer and does not guarantee removal of attachments, annotations, form data, scripts, or every embedded metadata field.

### Page organizer
- Page thumbnails, drag-and-drop and button reordering, multi-select.
- Rotate, duplicate, delete, reverse order, undo/redo.
- Save as copy, then preview, open or share the result.

### Batch processing
- Queue compress, watermark, protect, unlock, rotate and page-number jobs across many files.
- Per-file progress and status, cancel between files, retry failed items.

### Productivity
- **Presets**: save and reuse per-tool settings. Passwords and signature data are never stored.
- **Smart output naming**: pattern with `{OriginalName}`, `{Operation}`, `{Date}`, `{Counter}`. Names are sanitized and existing files are never overwritten. Applies to single tools and batch; leave blank for default names.

### Privacy
- **Offline & Privacy dashboard**: storage, cache and history usage; clear temporary files and local history.
- Activity-history retention: never, 7, 30 or 90 days. Retention removes history records only, not your PDFs.
- Legal screens: Privacy Policy and Terms.

## Tech stack

| Area | Technology |
|---|---|
| Framework | .NET 11 / .NET MAUI |
| UI toolkit | CommunityToolkit.Maui, Syncfusion.Maui.Toolkit |
| MVVM | CommunityToolkit.Mvvm |
| PDF editing | PDFsharp |
| PDF rendering | PDFtoImage + SkiaSharp (Android: native `PdfRenderer`) |
| Local storage | SQLite (`Microsoft.Data.Sqlite.Core`) and JSON files |
| Crypto | `System.Security.Cryptography.Pkcs` |

## Prerequisites

| Requirement | Notes |
|---|---|
| **.NET SDK 11.0** | Check with `dotnet --version` |
| **MAUI workload** | `dotnet workload install maui` |
| **Visual Studio 2026** (recommended) | With the ".NET Multi-platform App UI development" workload. VS Code with the .NET MAUI extension also works. |
| **Windows 10 1809+ / Windows 11** | Needed to build the Windows target. |
| **Android SDK** (API 24+; API 36 emulator recommended) | Installed with the VS workload, or via Android Studio. |
| **JDK 17+** | Installed with the VS workload. |
| **Mac with Xcode** | Only needed for iOS and Mac Catalyst. They cannot be built from Windows alone. |

Platform minimums: Android 7.0 (API 24), iOS 15, Mac Catalyst 17, Windows 10 build 17763.

## Getting started

```powershell
git clone https://github.com/awaneesh959891/Docusuite.git
cd Docusuite
dotnet workload install maui
dotnet restore
dotnet build -f net11.0-windows10.0.19041.0
```

Replace the target framework to build another platform:

| Platform | Target framework |
|---|---|
| Windows | `net11.0-windows10.0.19041.0` |
| Android | `net11.0-android` |
| iOS | `net11.0-ios` (macOS only) |
| Mac Catalyst | `net11.0-maccatalyst` (macOS only) |

> The project file is `MauiApp.csproj`; the assembly name is `DocSuite` and the Android application ID is `com.awaneesh.docsuite`.

## Running the app

### Visual Studio
1. Open `MauiApp.slnx`.
2. Pick the target in the toolbar: **Windows Machine**, an **Android emulator** or device.
3. Press **F5** (debug). Windows Debug runs unpackaged, so no installer is needed.

### Command line

```powershell
# Windows
dotnet run -f net11.0-windows10.0.19041.0

# Android (emulator or device must be connected: check with `adb devices`)
dotnet build -f net11.0-android -t:Run
```

### Android emulator tips
- Create an AVD in Visual Studio (Tools > Android > Android Device Manager) or Android Studio. Use API 31+ with x86_64 images.
- Allow at least 8 GB of free disk space on the drive that holds the AVD.
- If the emulator does not boot: use software rendering (`-gpu swiftshader_indirect`), lower RAM (`-memory 1536`), or cold boot with `-no-snapshot-load`.
- If an install fails with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, the installed app has a different signing key. Run `adb uninstall com.awaneesh.docsuite` and install again (this clears app data).

## Building release packages

Release artifacts are written to `artifacts\` and are git-ignored.

### Windows installer (MSIX)
```powershell
.\Installer\Build-WindowsInstaller.ps1
```
The MSIX is signed with a development certificate (`Installer\DocSuite.Dev.pfx`, git-ignored). Create your own test certificate, or use a trusted code-signing certificate for distribution. The `Install-DocSuite.ps1` / `Install-DocSuite.cmd` scripts install it for local testing.

### Android APK
```powershell
dotnet publish -f net11.0-android -c Release -p:AndroidPackageFormat=apk
```

### Android App Bundle for Google Play
```powershell
.\Installer\Build-AndroidPlayRelease.ps1 -KeystorePath "C:\secure\docsuite-upload.keystore" -StorePassword "<password>" -KeyPassword "<password>"
```
If no keystore exists, the script creates an upload keystore. **Never commit a keystore or its passwords, and back it up**: without it you cannot publish updates. Google Play also requires your own Play Console account, listing, privacy policy and (for new apps) Play App Signing.

### iOS / Mac Catalyst
Requires a Mac with Xcode, an Apple Developer account and provisioning profiles. Pair Visual Studio with the Mac, or build on the Mac:
```bash
dotnet build -f net11.0-ios -c Release
```

## Project structure

```
MauiApp.csproj        Project, packages, icon and splash
MauiProgram.cs        Startup and dependency injection
AppShell.xaml         Navigation shell
Pages/                XAML views
PageModels/           View models (MVVM)
Services/             PDF engines, workspace, presets, naming, history, rendering
Models/               Data types
Data/                 SQLite repositories (business workspace/admin features)
Behavior/, Utilities/ Helpers
Platforms/            Platform-specific code and manifests
Resources/            Icons, splash, fonts, images
Installer/            Build scripts, privacy policy and terms
```

Key services: `PdfPageRenderService` (rendering and PNG export), `DocumentWorkspaceService` (My Documents and recycle bin), `PdfPageOrganizerService`, `PdfPresetService`, `PdfOutputNamingService`, `HistoryService`.

## Configuration and local data

- No API keys, server or account are required to build or run.
- App data (history, presets in `pdf-presets.json`, thumbnail cache, recycle bin) lives in the app's private data folder on each device.
- Settings such as the output-name pattern and history retention are stored in app preferences.
- Signing keys, keystores and `.pfx` files are excluded by `.gitignore`. Provide your own.

## Troubleshooting

| Problem | Fix |
|---|---|
| `MauiApp is a namespace but used like a type` | Use the fully qualified `Microsoft.Maui.Hosting.MauiApp` where needed. |
| Workload errors | `dotnet workload restore` or `dotnet workload install maui`. |
| Build runs out of disk space | Free space on `C:`; NuGet cache and temp use it. |
| iOS target missing on Windows | Expected; iOS and Mac Catalyst need macOS. |
| Android preview is blank | Encrypted PDFs show a generic thumbnail until unlocked. |
| GitHub push rejected for large files | Do not commit `artifacts\`; use GitHub Releases or Git LFS for installers. |

## Roadmap

Implemented: workspace, recycle bin, page organizer, batch processing, privacy dashboard, presets, output naming, history retention, PDF to PNG/JPG, visual crop, basic metadata removal, Android preview fallback.

Still planned or unavailable: annotations, advanced scanning (edge detection, perspective correction), true redaction, encrypted vault (PIN/biometrics), OCR and full-text search, form filling, PDF comparison, signature profiles, command palette and shortcuts, completion screen with before/after sizes, Office and Markdown conversions, PDF/A, repair, resize/N-up/booklet, and hiding the admin tools behind an optional business workspace. These are intentionally not advertised as available until the app has a suitable offline implementation and can validate the result.

## Contributing

1. Fork the repository and create a feature branch.
2. Make focused changes and ensure `dotnet build` succeeds for your target.
3. Open a pull request describing the change and the platforms tested.

## Privacy and legal

DocSuite processes files locally and does not upload them. See `Installer\Privacy-Policy.txt` and `Installer\Terms-and-Disclaimer.txt`. Visual signatures are electronic images and are not certificate-based digital signatures. No license file is included yet; add one before accepting outside contributions.
