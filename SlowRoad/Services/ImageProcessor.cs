using SkiaSharp;

namespace SlowRoad.Services;

// Turns an uploaded file into the small set of image rows we actually serve.
//
// Two rules matter here:
//   1. The content type comes from the file's own first bytes, never from the
//      client. "image/svg+xml" in a Content-Type header is stored XSS, since
//      PhotosController serves these bytes back from our own origin.
//   2. Every width in Widths is really resized. Storing the original bytes
//      under several width labels made srcset a lie: phones downloaded the
//      full-size file anyway, and the database carried it more than once.
//
// SkiaSharp (MIT) does the decoding. ImageSharp was the obvious choice, but
// every 3.x release carries open high-severity decoder advisories and 4.x
// refuses to build in Release without a paid Six Labors license key.
public static class ImageProcessor
{
    // The widths the views ask for in srcset. Keep in sync with the <img> tags.
    public static readonly int[] Widths = { 700, 1400 };

    // Guard against decompression bombs: a 200 KB PNG can declare 30000x30000
    // and exhaust memory when decoded. Checked from the header first.
    public const long DefaultMaxPixels = 50_000_000;

    // JPEG/WebP quality. 82 is visually clean and roughly a third the size of 100.
    private const int Quality = 82;

    // One stored image: the width it was resized to, its type, and its bytes.
    public readonly record struct Variant(int Width, string ContentType, byte[] Bytes);

    // The type from the first bytes. null = not an image we accept.
    public static string? SniffContentType(byte[] b)
    {
        // JPEG: FF D8 FF
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return "image/png";

        // WebP: "RIFF" .... "WEBP"
        if (b.Length >= 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
            && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P')
            return "image/webp";

        return null;
    }

    // Resize one upload into the rows to store, largest last. Returns an empty
    // list when the bytes are not an allowed image or are too large to decode.
    //
    // Nothing is ever upscaled: a photo narrower than a target width is stored
    // once at its own width, and PhotosController falls back to the widest row
    // it finds. So a 500px upload yields one row, a 4000px upload yields 700
    // and 1400 and the 4000px original is never stored at all.
    public static IReadOnlyList<Variant> MakeVariants(byte[] original, long maxPixels = DefaultMaxPixels)
    {
        var contentType = SniffContentType(original);
        if (contentType is null)
            return Array.Empty<Variant>();

        // Header only - no pixels decoded yet, so a bomb is rejected cheaply.
        using var codec = SKCodec.Create(new MemoryStream(original));
        if (codec is null)
            return Array.Empty<Variant>();

        var origin = codec.EncodedOrigin;
        var info = codec.Info;
        if ((long)info.Width * info.Height > maxPixels)
            return Array.Empty<Variant>();

        using var decoded = SKBitmap.Decode(original);
        if (decoded is null)
            return Array.Empty<Variant>();

        // Phones write the real orientation into EXIF and leave the pixels
        // sideways. Bake it in; re-encoding drops the metadata (and its GPS).
        using var source = ApplyOrigin(decoded, origin);

        var targets = Widths.Where(w => w < source.Width).ToList();
        if (targets.Count < Widths.Length)
            targets.Add(source.Width);   // the original is the largest we can offer

        var format = contentType switch
        {
            "image/jpeg" => SKEncodedImageFormat.Jpeg,
            "image/png" => SKEncodedImageFormat.Png,
            _ => SKEncodedImageFormat.Webp,
        };

        var variants = new List<Variant>(targets.Count);
        foreach (var width in targets)
        {
            // Height follows the aspect ratio, at least 1px.
            var height = Math.Max(1, (int)Math.Round(source.Height * (double)width / source.Width));

            using var resized = source.Resize(
                new SKImageInfo(width, height, source.ColorType, source.AlphaType),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (resized is null)
                continue;

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(format, Quality);
            if (data is null)
                continue;

            variants.Add(new Variant(width, contentType, data.ToArray()));
        }

        return variants;
    }

    // Rotate/flip the pixels so the image is upright without EXIF.
    private static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft)
            return source;

        // The quarter turns swap width and height.
        var turned = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        var width = turned ? source.Height : source.Width;
        var height = turned ? source.Width : source.Height;

        var rotated = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(rotated);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:      // mirrored
                canvas.Scale(-1, 1, width / 2f, 0);
                break;
            case SKEncodedOrigin.BottomRight:   // 180
                canvas.RotateDegrees(180, width / 2f, height / 2f);
                break;
            case SKEncodedOrigin.BottomLeft:    // mirrored + 180
                canvas.Scale(1, -1, 0, height / 2f);
                break;
            case SKEncodedOrigin.LeftTop:       // mirrored + 90 CW
                canvas.Translate(width, height);
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1, source.Width / 2f, source.Height / 2f);
                canvas.Translate(-source.Width, -source.Height);
                break;
            case SKEncodedOrigin.RightTop:      // 90 CW
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:   // mirrored + 270 CW
                canvas.Scale(-1, 1, width / 2f, 0);
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom:    // 270 CW
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default);
        canvas.Flush();
        source.Dispose();
        return rotated;
    }
}
