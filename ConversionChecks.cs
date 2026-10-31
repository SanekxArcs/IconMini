using SkiaSharp;

namespace IconMini;

internal static class ConversionChecks
{
    public static int Run()
    {
        string temp = Path.Combine(Path.GetTempPath(), "IconMini-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            using var source = new SKBitmap(160, 80);
            using (var canvas = new SKCanvas(source)) canvas.Clear(SKColors.Red);
            using (var file = File.Create(Path.Combine(temp, "source.png"))) source.Encode(file, SKEncodedImageFormat.Png, 100);
            using var renderer = new IconRenderer(Path.Combine(temp, "source.png"));
            foreach (bool folder in new[] { false, true })
            {
                byte[] ico = renderer.CreateIco(folder, false, 0, SKColors.Transparent);
                using var reader = new BinaryReader(new MemoryStream(ico));
                Check(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1, "ICO header");
                int count = reader.ReadUInt16();
                var expected = folder ? IconRenderer.FolderSizes : IconRenderer.FaviconSizes;
                Check(count == expected.Length, "ICO image count");
                for (int i = 0; i < count; i++)
                {
                    int width = reader.ReadByte(); int height = reader.ReadByte();
                    Check((width == 0 ? 256 : width) == expected[i] && width == height, "ICO dimensions");
                    reader.ReadUInt16(); Check(reader.ReadUInt16() == 1 && reader.ReadUInt16() == 32, "ICO depth");
                    int length = reader.ReadInt32(); int offset = reader.ReadInt32();
                    Check(offset >= 6 + count * 16 && offset + length <= ico.Length, "ICO bounds");
                    using var image = SKBitmap.Decode(ico.AsSpan(offset, length).ToArray());
                    Check(image is not null && image.Width == expected[i] && image.Height == expected[i], "PNG frame decoding");
                }
                File.WriteAllBytes(Path.Combine(temp, folder ? "folder.ico" : "favicon.ico"), ico);
            }
            using var fit = SKBitmap.Decode(renderer.RenderPng(32, false, 0, SKColors.Transparent));
            Check(fit.GetPixel(0, 0).Alpha == 0 && fit.GetPixel(16, 16).Red == 255, "Fit preserves transparency");
            using var crop = SKBitmap.Decode(renderer.RenderPng(32, true, 0, SKColors.Transparent));
            Check(crop.GetPixel(0, 0).Alpha == 255, "Crop fills square");
            using var padded = SKBitmap.Decode(renderer.RenderPng(32, true, 25, SKColors.White));
            Check(padded.GetPixel(0, 0) == SKColors.White && padded.GetPixel(16, 16).Red == 255, "Padding and background");
            File.WriteAllText(Path.Combine(temp, "vector.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><circle cx='50' cy='50' r='40' fill='#00ff00'/></svg>");
            using var svg = new IconRenderer(Path.Combine(temp, "vector.svg"));
            using var vector = SKBitmap.Decode(svg.RenderPng(256, false, 0, SKColors.Transparent));
            Check(vector.GetPixel(128, 128).Green == 255 && vector.GetPixel(0, 0).Alpha == 0, "SVG rasterization");
            File.WriteAllText(Path.Combine(temp, "invalid.png"), "not an image");
            bool rejected = false;
            try { using var invalid = new IconRenderer(Path.Combine(temp, "invalid.png")); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Reject corrupt input");
            File.WriteAllText(Path.Combine(temp, "external.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><image href='https://example.com/a.png'/></svg>");
            rejected = false;
            try { using var external = new IconRenderer(Path.Combine(temp, "external.svg")); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Reject external SVG resources");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-result.txt"), "PASS: ICO frames, sizes, PNG decoding, transparency, crop, padding, SVG, invalid input, external resource rejection.");
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-result.txt"), "FAIL: " + ex);
            return 1;
        }
        finally { Directory.Delete(temp, true); }
    }
    private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
}
