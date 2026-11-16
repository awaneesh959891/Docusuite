using MauiApp.Models;
using SkiaSharp;

namespace MauiApp.Services;

// Image helpers shared by the scan, signature, watermark and image-to-PDF features.
public static class ImageTools
{
    public static SKBitmap DecodeOriented(byte[] data, SKAlphaType alpha = SKAlphaType.Premul)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(data))
            ?? throw new InvalidOperationException("This image format isn't supported.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, alpha);
        var bmp = new SKBitmap(info);
        var result = codec.GetPixels(info, bmp.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
        {
            bmp.Dispose();
            throw new InvalidOperationException("The image could not be decoded.");
        }
        var oriented = ApplyOrigin(bmp, codec.EncodedOrigin);
        if (!ReferenceEquals(oriented, bmp))
            bmp.Dispose();
        return oriented;
    }

    private static SKBitmap ApplyOrigin(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft || origin == SKEncodedOrigin.Default)
            return src;

        float w = src.Width, h = src.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // x' = sx*x + kx*y + tx ; y' = ky*x + sy*y + ty
        var m = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };

        var dst = new SKBitmap(new SKImageInfo(swap ? src.Height : src.Width, swap ? src.Width : src.Height,
            src.ColorType, src.AlphaType));
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(m);
        canvas.DrawBitmap(src, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        return dst;
    }

    public static SKBitmap Downscale(SKBitmap src, int maxSide)
    {
        var longest = Math.Max(src.Width, src.Height);
        if (longest <= maxSide)
            return src;
        var scale = (double)maxSide / longest;
        var info = new SKImageInfo(Math.Max(1, (int)(src.Width * scale)), Math.Max(1, (int)(src.Height * scale)),
            src.ColorType, src.AlphaType);
        var dst = src.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidOperationException("Could not resize the image.");
        src.Dispose();
        return dst;
    }

    public static byte[] Encode(SKBitmap bmp, SKEncodedImageFormat format, int quality)
    {
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    // JPEG/PNG straight from disk when PDFsharp can embed them as-is; otherwise re-encoded (EXIF rotation, WebP, etc.).
    public static byte[] NormalizeForPdf(string path)
    {
        var raw = File.ReadAllBytes(path);
        using var codec = SKCodec.Create(SKData.CreateCopy(raw));
        if (codec is null)
            throw new InvalidOperationException($"'{Path.GetFileName(path)}' is not a supported image.");

        var plain = codec.EncodedOrigin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default
            && codec.EncodedFormat is SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png;
        if (plain)
            return raw;

        using var bmp = DecodeOriented(raw);
        var hasAlpha = HasTransparency(bmp);
        return hasAlpha ? Encode(bmp, SKEncodedImageFormat.Png, 100) : Encode(bmp, SKEncodedImageFormat.Jpeg, 92);
    }

    public static bool HasTransparency(SKBitmap bmp)
    {
        var span = bmp.GetPixelSpan();
        for (int i = 3; i < span.Length; i += 4)
            if (span[i] < 250)
                return true;
        return false;
    }

    // ---------- Scanning ----------

    public static SKBitmap AutoCrop(SKBitmap src)
    {
        const int probe = 400;
        var scale = Math.Min(1.0, (double)probe / Math.Max(src.Width, src.Height));
        var pw = Math.Max(1, (int)(src.Width * scale));
        var ph = Math.Max(1, (int)(src.Height * scale));
        using var small = src.Resize(new SKImageInfo(pw, ph, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (small is null)
            return src;

        var gray = ToGray(small);
        var threshold = Otsu(gray);

        double border = 0;
        int count = 0;
        for (int x = 0; x < pw; x++) { border += gray[x] + gray[(ph - 1) * pw + x]; count += 2; }
        for (int y = 0; y < ph; y++) { border += gray[y * pw] + gray[y * pw + pw - 1]; count += 2; }
        var borderIsDark = border / count < threshold;

        bool IsDoc(byte v) => borderIsDark ? v > threshold : v <= threshold;

        var colCounts = new int[pw];
        var rowCounts = new int[ph];
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
                if (IsDoc(gray[y * pw + x])) { rowCounts[y]++; colCounts[x]++; }

        int top = FirstAbove(rowCounts, pw * 0.3, false), bottom = FirstAbove(rowCounts, pw * 0.3, true);
        int left = FirstAbove(colCounts, ph * 0.3, false), right = FirstAbove(colCounts, ph * 0.3, true);
        if (top < 0 || left < 0 || bottom <= top || right <= left)
            return src;

        var areaRatio = (double)((right - left) * (bottom - top)) / (pw * ph);
        if (areaRatio < 0.25 || areaRatio > 0.97)
            return src;

        var padX = (int)(pw * 0.01) + 1;
        var padY = (int)(ph * 0.01) + 1;
        var rect = SKRectI.Create(
            Math.Max(0, (int)((left - padX) / scale)),
            Math.Max(0, (int)((top - padY) / scale)),
            0, 0);
        var x2 = Math.Min(src.Width, (int)((right + padX) / scale));
        var y2 = Math.Min(src.Height, (int)((bottom + padY) / scale));
        rect = new SKRectI(rect.Left, rect.Top, x2, y2);

        var cropped = new SKBitmap(rect.Width, rect.Height, src.ColorType, src.AlphaType);
        if (!src.ExtractSubset(cropped, rect))
        {
            cropped.Dispose();
            return src;
        }
        var copy = cropped.Copy();
        cropped.Dispose();
        src.Dispose();
        return copy;
    }

    private static int FirstAbove(int[] counts, double min, bool fromEnd)
    {
        if (fromEnd)
        {
            for (int i = counts.Length - 1; i >= 0; i--)
                if (counts[i] >= min) return i;
        }
        else
        {
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] >= min) return i;
        }
        return -1;
    }

    private static byte[] ToGray(SKBitmap bmp)
    {
        var span = bmp.GetPixelSpan();
        var gray = new byte[bmp.Width * bmp.Height];
        for (int i = 0, p = 0; i < gray.Length; i++, p += 4)
            gray[i] = (byte)((span[p] * 299 + span[p + 1] * 587 + span[p + 2] * 114) / 1000);
        return gray;
    }

    private static int Otsu(byte[] gray)
    {
        var hist = new int[256];
        foreach (var g in gray) hist[g]++;
        long total = gray.Length, sum = 0;
        for (int i = 0; i < 256; i++) sum += (long)i * hist[i];
        long sumB = 0, wB = 0;
        double best = -1;
        int threshold = 127;
        for (int t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            var wF = total - wB;
            if (wF == 0) break;
            sumB += (long)t * hist[t];
            double mB = (double)sumB / wB, mF = (double)(sum - sumB) / wF;
            var between = (double)wB * wF * (mB - mF) * (mB - mF);
            if (between > best) { best = between; threshold = t; }
        }
        return threshold;
    }

    public static SKBitmap ApplyFilter(SKBitmap src, ScanFilter filter)
    {
        switch (filter)
        {
            case ScanFilter.Original:
                return src;
            case ScanFilter.Grayscale:
                return ColorMatrix(src, Gray(1f, 0f));
            case ScanFilter.Enhanced:
                return ColorMatrix(src, Contrast(1.35f, -0.08f));
            default:
                return BlackAndWhite(src);
        }
    }

    private static float[] Gray(float scale, float offset) =>
    [
        0.299f * scale, 0.587f * scale, 0.114f * scale, 0, offset,
        0.299f * scale, 0.587f * scale, 0.114f * scale, 0, offset,
        0.299f * scale, 0.587f * scale, 0.114f * scale, 0, offset,
        0, 0, 0, 1, 0
    ];

    private static float[] Contrast(float c, float b) =>
    [
        c, 0, 0, 0, b,
        0, c, 0, 0, b,
        0, 0, c, 0, b,
        0, 0, 0, 1, 0
    ];

    private static SKBitmap ColorMatrix(SKBitmap src, float[] matrix)
    {
        var dst = new SKBitmap(src.Width, src.Height, src.ColorType, SKAlphaType.Premul);
        using var canvas = new SKCanvas(dst);
        using var paint = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(matrix) };
        canvas.DrawBitmap(src, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint);
        src.Dispose();
        return dst;
    }

    // Adaptive threshold: a pixel is ink when it is clearly darker than its local neighbourhood.
    private static SKBitmap BlackAndWhite(SKBitmap src)
    {
        int w = src.Width, h = src.Height;
        var gray = ToGray(src);
        var integral = new int[(w + 1) * (h + 1)];
        for (int y = 0; y < h; y++)
        {
            int rowSum = 0;
            for (int x = 0; x < w; x++)
            {
                rowSum += gray[y * w + x];
                integral[(y + 1) * (w + 1) + x + 1] = integral[y * (w + 1) + x + 1] + rowSum;
            }
        }

        int r = Math.Max(8, Math.Max(w, h) / 32);
        var dst = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var span = dst.GetPixelSpan();
        for (int y = 0; y < h; y++)
        {
            int y0 = Math.Max(0, y - r), y1 = Math.Min(h, y + r + 1);
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - r), x1 = Math.Min(w, x + r + 1);
                long sum = (long)integral[y1 * (w + 1) + x1] - integral[y0 * (w + 1) + x1]
                    - integral[y1 * (w + 1) + x0] + integral[y0 * (w + 1) + x0];
                var mean = sum / (double)((x1 - x0) * (y1 - y0));
                byte v = gray[y * w + x] < mean * 0.9 ? (byte)0 : (byte)255;
                int p = (y * w + x) * 4;
                span[p] = span[p + 1] = span[p + 2] = v;
                span[p + 3] = 255;
            }
        }
        src.Dispose();
        return dst;
    }

    // ---------- Signatures & watermarks ----------

    public static byte[] SignatureFromText(string text, SKColor color)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Type your name to create a signature.");

        using var typeface = PickScriptTypeface();
        using var font = new SKFont(typeface, 120);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        var width = font.MeasureText(text, paint);
        font.GetFontMetrics(out var metrics);
        var pad = 40;
        var info = new SKImageInfo((int)Math.Ceiling(width) + pad * 2, (int)Math.Ceiling(metrics.Descent - metrics.Ascent) + pad * 2,
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawText(text, pad, pad - metrics.Ascent, SKTextAlign.Left, font, paint);
        }
        return EncodeTrimmed(bmp);
    }

    private static SKTypeface PickScriptTypeface()
    {
        foreach (var family in new[] { "Segoe Script", "Snell Roundhand", "Brush Script MT", "Lucida Handwriting", "cursive" })
        {
            var face = SKTypeface.FromFamilyName(family, SKFontStyle.Italic);
            if (face is not null && string.Equals(face.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                return face;
            face?.Dispose();
        }
        return SKTypeface.FromFamilyName(null, SKFontStyle.Italic);
    }

    public static byte[] SignatureFromStrokes(IReadOnlyList<IReadOnlyList<(float X, float Y)>> strokes, SKColor color, float strokeWidth = 4f)
    {
        var points = strokes.Where(s => s.Count > 0).ToList();
        if (points.Count == 0)
            throw new InvalidOperationException("Draw your signature first.");

        var minX = points.Min(s => s.Min(p => p.X));
        var minY = points.Min(s => s.Min(p => p.Y));
        var maxX = points.Max(s => s.Max(p => p.X));
        var maxY = points.Max(s => s.Max(p => p.Y));
        const float scale = 3f;
        var pad = 20f;
        var info = new SKImageInfo((int)((maxX - minX) * scale + pad * 2) + 1, (int)((maxY - minY) * scale + pad * 2) + 1,
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint
            {
                Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth * scale, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
            };
            foreach (var stroke in points)
            {
                float X(float v) => (v - minX) * scale + pad;
                float Y(float v) => (v - minY) * scale + pad;
                if (stroke.Count == 1)
                {
                    canvas.DrawPoint(X(stroke[0].X), Y(stroke[0].Y), paint);
                    continue;
                }
                using var path = new SKPath();
                path.MoveTo(X(stroke[0].X), Y(stroke[0].Y));
                for (int i = 1; i < stroke.Count; i++)
                    path.LineTo(X(stroke[i].X), Y(stroke[i].Y));
                canvas.DrawPath(path, paint);
            }
        }
        return EncodeTrimmed(bmp);
    }

    public static byte[] SignatureFromImage(byte[] data)
    {
        using var bmp = Downscale(DecodeOriented(data, SKAlphaType.Unpremul), 900);
        if (!HasTransparency(bmp))
        {
            // Key out the paper so only the ink remains.
            var span = bmp.GetPixelSpan();
            for (int i = 0; i < span.Length; i += 4)
            {
                var lum = (span[i] * 299 + span[i + 1] * 587 + span[i + 2] * 114) / 1000;
                span[i + 3] = lum >= 235 ? (byte)0 : lum <= 190 ? (byte)255 : (byte)((235 - lum) * 255 / 45);
            }
        }
        return EncodeTrimmed(bmp);
    }

    private static byte[] EncodeTrimmed(SKBitmap bmp)
    {
        var span = bmp.GetPixelSpan();
        int w = bmp.Width, h = bmp.Height, minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (span[(y * w + x) * 4 + 3] > 8)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }

        if (maxX < 0)
            throw new InvalidOperationException("The signature image appears to be empty.");

        const int pad = 4;
        var rect = new SKRectI(Math.Max(0, minX - pad), Math.Max(0, minY - pad), Math.Min(w, maxX + pad + 1), Math.Min(h, maxY + pad + 1));
        using var cropped = new SKBitmap(rect.Width, rect.Height, bmp.ColorType, bmp.AlphaType);
        bmp.ExtractSubset(cropped, rect);
        using var copy = cropped.Copy();
        return Encode(copy, SKEncodedImageFormat.Png, 100);
    }

    public static byte[] WatermarkImage(byte[] data, double opacity)
    {
        using var bmp = Downscale(DecodeOriented(data, SKAlphaType.Unpremul), 1000);
        var span = bmp.GetPixelSpan();
        var factor = Math.Clamp(opacity, 0.02, 1.0);
        for (int i = 3; i < span.Length; i += 4)
            span[i] = (byte)(span[i] * factor);
        return Encode(bmp, SKEncodedImageFormat.Png, 100);
    }
}
