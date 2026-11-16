# DocSuite

Cross-platform, offline-first PDF management app built with .NET MAUI for Windows, Android, and iOS.

## Offline workflow features

- Save and reuse per-tool PDF presets. Presets do not store passwords, signature images, or signature strokes.
- Customize generated filenames with `{OriginalName}`, `{Operation}`, `{Date}`, and `{Counter}` tokens. Names are sanitized and existing output files are not overwritten. The shared setting applies to single-tool and batch operations; leave it blank to keep each tool's normal name.
- Configure local activity-history retention (never, 7, 30, or 90 days). Retention removes activity records only, not generated PDFs.
- Export each PDF page to a PNG image. Rendering uses the native Android PDF renderer on Android and the existing PDF rendering library on other platforms.
