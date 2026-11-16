namespace MauiApp.Services;

// Wraps FilePicker. On Windows the WinRT picker throws "Unspecified error" when the app runs elevated,
// so we fall back to the classic Win32 open dialog, which works in both cases.
public static class FilePickerHelper
{
    public static async Task<List<FileResult>> PickAsync(PickOptions options, bool multiple)
    {
        try
        {
            if (multiple)
                return (await FilePicker.Default.PickMultipleAsync(options))?.ToList() ?? [];
            return await FilePicker.Default.PickAsync(options) is { } one ? [one] : [];
        }
#if WINDOWS
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            return Win32Dialog.Pick(options, multiple);
        }
#else
        catch (OperationCanceledException)
        {
            return [];
        }
#endif
    }

    public static async Task<FileResult?> PickOneAsync(PickOptions options) =>
        (await PickAsync(options, false)).FirstOrDefault();

#if WINDOWS
    private static class Win32Dialog
    {
        private const int OfnAllowMultiSelect = 0x200, OfnExplorer = 0x80000, OfnFileMustExist = 0x1000,
            OfnPathMustExist = 0x800, OfnNoChangeDir = 0x8;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string? lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public string? lpstrFileTitle;
            public int nMaxFileTitle;
            public string? lpstrInitialDir;
            public string? lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string? lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string? lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [System.Runtime.InteropServices.DllImport("comdlg32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        public static List<FileResult> Pick(PickOptions options, bool multiple)
        {
            var patterns = options.FileTypes?.Value?.ToList() ?? [];
            var filter = patterns.Count > 0
                ? $"Supported files\0{string.Join(';', patterns.Select(p => p.StartsWith('*') ? p : "*" + p))}\0All files\0*.*\0\0"
                : "All files\0*.*\0\0";

            const int bufferChars = 32768;
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(bufferChars * 2);
            try
            {
                System.Runtime.InteropServices.Marshal.WriteInt16(buffer, 0);
                var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                var ofn = new OpenFileName
                {
                    lStructSize = System.Runtime.InteropServices.Marshal.SizeOf<OpenFileName>(),
                    hwndOwner = window is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(window),
                    lpstrFilter = filter,
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = bufferChars,
                    lpstrTitle = options.PickerTitle,
                    Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | (multiple ? OfnAllowMultiSelect : 0)
                };

                if (!GetOpenFileNameW(ref ofn))
                    return [];

                var parts = new List<string>();
                var offset = 0;
                while (offset < bufferChars)
                {
                    var s = System.Runtime.InteropServices.Marshal.PtrToStringUni(buffer + offset * 2) ?? string.Empty;
                    if (s.Length == 0)
                        break;
                    parts.Add(s);
                    offset += s.Length + 1;
                }

                if (parts.Count == 0)
                    return [];
                if (parts.Count == 1)
                    return [new FileResult(parts[0])];
                return parts.Skip(1).Select(name => new FileResult(Path.Combine(parts[0], name))).ToList();
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
            }
        }
    }
#endif
}
