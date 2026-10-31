using SkiaSharp;
using Svg.Skia;
using System.Xml.Linq;

namespace IconMini;

internal sealed class IconRenderer : IDisposable
{
    private readonly SKBitmap? bitmap;
    private readonly SKSvg? svg;
    public float Width { get; }
    public float Height { get; }
    public string SourcePath { get; }
    public static readonly int[] FaviconSizes = [16, 32, 48];
    public static readonly int[] FolderSizes = [16, 24, 32, 48, 64, 128, 256];

    public IconRenderer(string path)
    {
        SourcePath = path;
        if (new FileInfo(path).Length > 80 * 1024 * 1024)
            throw new InvalidDataException("Зображення завелике. Оберіть файл до 80 МБ.");
        if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            // Keep SVG input local: reject document types and external references.
            using var xmlReader = System.Xml.XmlReader.Create(path,
                new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
            var xml = XDocument.Load(xmlReader);
            if (xml.Descendants().Attributes().Any(a =>
                a.Name.LocalName == "href" && !a.Value.StartsWith('#') && !a.Value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                || xml.ToString().Contains("url(", StringComparison.OrdinalIgnoreCase)
                    && System.Text.RegularExpressions.Regex.IsMatch(xml.ToString(), @"url\(\s*['"" ]*(https?:|file:|//)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new InvalidDataException("SVG має зовнішні ресурси. Вбудуйте їх у файл перед імпортом.");
            svg = new SKSvg();
            try
            {
                svg.Load(path);
                var bounds = svg.Picture?.CullRect ?? SKRect.Empty;
                Width = bounds.Width;
                Height = bounds.Height;
                if (Width <= 0 || Height <= 0) throw new InvalidDataException("SVG не має видимого зображення.");
            }
            catch { svg.Dispose(); throw; }
        }
        else
        {
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream) ?? throw new InvalidDataException("Цей формат не підтримується. Спробуйте PNG, JPG, WebP, SVG, GIF або BMP.");
            if ((long)codec.Info.Width * codec.Info.Height > 64_000_000)
                throw new InvalidDataException("Зображення завелике. Максимум — 64 мегапікселі.");
            using var decoded = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Не вдалося прочитати зображення.");
            bitmap = Orient(decoded, codec.EncodedOrigin);
            Width = bitmap.Width;
            Height = bitmap.Height;
        }
    }

    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        bool swap = (int)origin >= 5;
        var result = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(result.Width, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(result.Width, result.Height); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, result.Height); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(result.Width, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(result.Width, result.Height); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, result.Height); canvas.RotateDegrees(-90); break;
        }
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    public byte[] RenderPng(int size, bool crop, int padding, SKColor background)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(background);
        float inset = size * padding / 100f;
        float available = size - inset * 2;
        canvas.ClipRect(new SKRect(inset, inset, size - inset, size - inset));
        float scale = crop ? Math.Max(available / Width, available / Height) : Math.Min(available / Width, available / Height);
        canvas.Translate((size - Width * scale) / 2, (size - Height * scale) / 2);
        canvas.Scale(scale);
        if (svg?.Picture is { } picture)
        {
            canvas.Translate(-picture.CullRect.Left, -picture.CullRect.Top);
            canvas.DrawPicture(picture);
        }
        else
        {
            using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
            canvas.DrawBitmap(bitmap!, 0, 0, paint);
        }
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public byte[] CreateIco(bool folder, bool crop, int padding, SKColor background)
    {
        int[] sizes = folder ? FolderSizes : FaviconSizes;
        var images = sizes.Select(s => RenderPng(s, crop, padding, background)).ToArray();
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
        int offset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(images[i].Length); writer.Write(offset);
            offset += images[i].Length;
        }
        foreach (var data in images) writer.Write(data);
        return output.ToArray();
    }

    public void Dispose() { bitmap?.Dispose(); svg?.Dispose(); }
}
