using HomeApp.Expenses.Controllers;
using HomeApp.Videos.Services;
using ImageMagick;
using HomeApp.Library.Imaging;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeApp.Expenses.Tests;

public sealed class ImageConversionTests
{
    [Theory]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.Gif)]
    [InlineData(MagickFormat.Bmp)]
    [InlineData(MagickFormat.Tiff)]
    [InlineData(MagickFormat.WebP)]
    public void RasterDecodersAcceptSupportedFormats(MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.White, 10, 20);
        using var decoded = RasterImage.Read(image.ToByteArray(format));
        Assert.Equal(10u, decoded.Width);
        Assert.Equal(20u, decoded.Height);
    }

    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'></svg>")]
    [InlineData("%PDF-1.7")]
    public void NonRasterFormatsAreRejectedBeforeDecoding(string input)
    {
        Assert.Throws<NotSupportedException>(() => RasterImage.Read(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(3200, 1600, 1600, 800)]
    [InlineData(800, 2400, 533, 1600)]
    [InlineData(200, 100, 200, 100)]
    public async Task ReceiptImagesAreJpegAndFitWithoutUpscaling(int width, int height, int expectedWidth, int expectedHeight)
    {
        using var image = new MagickImage(MagickColors.White, (uint)width, (uint)height);
        var bytes = image.ToByteArray(MagickFormat.Png);
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "receipt", "synthetic.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        var result = await ExpensesController.ProcessReceiptImageAsync(file, default);
        Assert.Equal("image/jpeg", result.ContentType);
        using var converted = new MagickImage(result.Bytes.Span);
        Assert.Equal(MagickFormat.Jpeg, converted.Format);
        Assert.Equal((uint)expectedWidth, converted.Width);
        Assert.Equal((uint)expectedHeight, converted.Height);
        Assert.Null(converted.GetProfile("exif"));
    }

    [Theory]
    [InlineData(400, 200, 200, 100)]
    [InlineData(40, 20, 40, 20)]
    [InlineData(1000, 1, 200, 1)]
    public void ThumbnailsPreserveAspectRatioAndMinimumHeight(int width, int height, int expectedWidth, int expectedHeight)
    {
        using var image = new MagickImage(MagickColors.White, (uint)width, (uint)height);
        using var http = new HttpClient();
        var service = new ImageService(http, NullLogger<ImageService>.Instance);
        var bytes = service.Optimize(image.ToByteArray(MagickFormat.Png));
        using var thumbnail = new MagickImage(bytes);
        Assert.Equal(MagickFormat.Jpeg, thumbnail.Format);
        Assert.Equal((uint)expectedWidth, thumbnail.Width);
        Assert.Equal((uint)expectedHeight, thumbnail.Height);
    }
}
