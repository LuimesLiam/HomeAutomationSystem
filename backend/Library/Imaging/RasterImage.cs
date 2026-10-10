using ImageMagick;

namespace HomeApp.Library.Imaging;

public static class RasterImage
{
    // Select a raster decoder from the bytes before parsing. SVG, PDF and other
    // delegate-based formats must not become accepted receipt/thumbnail inputs.
    public static MagickImage Read(ReadOnlySpan<byte> bytes)
    {
        var format = bytes switch
        {
            _ when bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }) => MagickFormat.Jpeg,
            _ when bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }) => MagickFormat.Png,
            _ when bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8) => MagickFormat.Gif,
            _ when bytes.StartsWith("BM"u8) => MagickFormat.Bmp,
            _ when bytes.StartsWith("II\u002a\0"u8) || bytes.StartsWith("MM\0\u002a"u8) => MagickFormat.Tiff,
            _ when bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8) => MagickFormat.WebP,
            _ => throw new NotSupportedException("Only JPEG, PNG, GIF, BMP, TIFF and WebP images can be converted.")
        };
        return new MagickImage(bytes, new MagickReadSettings { Format = format });
    }
}
