using SkiaSharp;
using SlowRoad.Services;

namespace SlowRoad.Tests;

// The upload path: what we accept, and what we store once we accept it.
public class ImageProcessorTests
{
    // A real encoded image of the given size, so the decoder has something true
    // to read (hand-written headers would be rejected before resizing).
    internal static byte[] MakeImage(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        // Something other than flat colour, so resizing has real work to do.
        using var paint = new SKPaint { Color = SKColors.Goldenrod };
        canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static (int Width, int Height) SizeOf(byte[] bytes)
    {
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        return (codec.Info.Width, codec.Info.Height);
    }

    [Fact] // a JPEG is recognised by its first bytes
    public void Sniff_Jpeg()
        => Assert.Equal("image/jpeg", ImageProcessor.SniffContentType(MakeImage(10, 10)));

    [Fact] // a PNG is recognised by its first bytes
    public void Sniff_Png()
        => Assert.Equal("image/png", ImageProcessor.SniffContentType(MakeImage(10, 10, SKEncodedImageFormat.Png)));

    [Fact] // a WebP is recognised by its first bytes
    public void Sniff_Webp()
        => Assert.Equal("image/webp", ImageProcessor.SniffContentType(MakeImage(10, 10, SKEncodedImageFormat.Webp)));

    [Fact] // SVG is the stored-XSS case: it must never be accepted
    public void Sniff_RejectsSvg()
    {
        var svg = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        Assert.Null(ImageProcessor.SniffContentType(svg));
        Assert.Empty(ImageProcessor.MakeVariants(svg));
    }

    [Fact] // an image/png Content-Type on SVG bytes changes nothing - we read the bytes
    public void Sniff_IgnoresClaimedTypeAndReadsBytes()
    {
        var html = System.Text.Encoding.UTF8.GetBytes("<html><body>not an image</body></html>");
        Assert.Null(ImageProcessor.SniffContentType(html));
        Assert.Empty(ImageProcessor.MakeVariants(html));
    }

    [Fact] // empty and truncated input must not throw
    public void Sniff_HandlesShortInput()
    {
        Assert.Null(ImageProcessor.SniffContentType(Array.Empty<byte>()));
        Assert.Null(ImageProcessor.SniffContentType(new byte[] { 0xFF, 0xD8 }));
        Assert.Empty(ImageProcessor.MakeVariants(Array.Empty<byte>()));
    }

    [Fact] // the point of the whole exercise: 700 and 1400 are really 700 and 1400
    public void MakeVariants_ResizesToEachWidth()
    {
        var variants = ImageProcessor.MakeVariants(MakeImage(4000, 2500));

        Assert.Equal(new[] { 700, 1400 }, variants.Select(v => v.Width).ToArray());
        foreach (var variant in variants)
        {
            var (width, height) = SizeOf(variant.Bytes);
            Assert.Equal(variant.Width, width);
            // Aspect ratio kept (4000x2500 is 1.6:1).
            Assert.Equal(variant.Width / 1.6, height, 1.0);
        }
    }

    [Fact] // the full-size original is never stored
    public void MakeVariants_DropsTheOriginal()
    {
        var original = MakeImage(4000, 2500);
        var variants = ImageProcessor.MakeVariants(original);

        Assert.DoesNotContain(4000, variants.Select(v => v.Width));
        Assert.All(variants, v => Assert.True(
            v.Bytes.Length < original.Length,
            $"{v.Width}w is {v.Bytes.Length} bytes, original is {original.Length}"));
    }

    [Fact] // two rows must not hold the same bytes - that was the bug
    public void MakeVariants_RowsDiffer()
    {
        var variants = ImageProcessor.MakeVariants(MakeImage(4000, 2500));

        Assert.Equal(2, variants.Count);
        Assert.NotEqual(variants[0].Bytes.Length, variants[1].Bytes.Length);
        Assert.True(variants[0].Bytes.Length < variants[1].Bytes.Length,
            "the 700w row should be smaller than the 1400w row");
    }

    [Fact] // a photo narrower than 1400 is stored at its own width, not upscaled
    public void MakeVariants_DoesNotUpscale()
    {
        var variants = ImageProcessor.MakeVariants(MakeImage(1000, 800));

        Assert.Equal(new[] { 700, 1000 }, variants.Select(v => v.Width).ToArray());
        Assert.Equal(1000, SizeOf(variants[1].Bytes).Width);
    }

    [Fact] // a tiny photo yields exactly one row (no duplicate-width rows)
    public void MakeVariants_SmallImageYieldsOneRow()
    {
        var variants = ImageProcessor.MakeVariants(MakeImage(400, 300));

        Assert.Single(variants);
        Assert.Equal(400, variants[0].Width);
        Assert.Equal(400, SizeOf(variants[0].Bytes).Width);
    }

    [Fact] // the stored type matches the source format
    public void MakeVariants_KeepsFormat()
    {
        Assert.All(ImageProcessor.MakeVariants(MakeImage(2000, 1000, SKEncodedImageFormat.Png)),
            v => Assert.Equal("image/png", v.ContentType));
        Assert.All(ImageProcessor.MakeVariants(MakeImage(2000, 1000, SKEncodedImageFormat.Webp)),
            v => Assert.Equal("image/webp", v.ContentType));
    }

    [Fact] // a decompression bomb is rejected from the header, before decoding
    public void MakeVariants_RejectsTooManyPixels()
    {
        // 2000x1000 = 2,000,000 pixels, against a 1,000,000 pixel ceiling.
        Assert.Empty(ImageProcessor.MakeVariants(MakeImage(2000, 1000), maxPixels: 1_000_000));
    }

    [Fact] // the widths we generate are the widths the views ask for in srcset
    public void Widths_MatchTheViews()
    {
        var card = File.ReadAllText(Path.Combine(ProjectRoot(), "SlowRoad", "Views", "Home", "_PlaceCard.cshtml"));

        foreach (var width in ImageProcessor.Widths)
            Assert.Contains($"/{width} {width}w", card);
    }

    // Walk up from the test binary to the folder holding the two projects.
    private static string ProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SlowRoad.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
