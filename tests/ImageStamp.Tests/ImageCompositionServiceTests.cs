using ImageStamp.Core.Models;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace ImageStamp.Tests;

public sealed class ImageCompositionServiceTests
{
    [Fact]
    public async Task ComposeAsync_WithoutLayers_ReturnsTheBaseImage()
    {
        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(10, 10, TestImages.Red);

        CompositionResult result = await TestServices.CompositionService()
            .ComposeAsync(request, CancellationToken.None);

        Assert.Equal(10, result.Width);
        Assert.Equal(10, result.Height);
        Assert.IsType<PngFormat>(Image.DetectFormat(result.Png));

        using Image<Rgba32> output = TestImages.Decode(result.Png);
        Assert.Equal(TestImages.Red, output[0, 0]);
        Assert.Equal(TestImages.Red, output[9, 9]);
    }

    [Fact]
    public async Task ComposeAsync_WithAnOpaqueImageLayer_DrawsItAtTheRequestedPosition()
    {
        Layer overlay = new Layer();
        overlay.Type = LayerTypes.Image;
        overlay.X = 3;
        overlay.Y = 3;
        overlay.ZIndex = 1;
        overlay.Opacity = 1f;
        overlay.Image = TestImages.SolidPngStream(2, 2, TestImages.Blue);

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(10, 10, TestImages.Red);
        request.Layers.Add(overlay);

        CompositionResult result = await TestServices.CompositionService()
            .ComposeAsync(request, CancellationToken.None);

        using Image<Rgba32> output = TestImages.Decode(result.Png);

        Assert.Equal(TestImages.Blue, output[3, 3]);
        Assert.Equal(TestImages.Blue, output[4, 4]);
        Assert.Equal(TestImages.Red, output[0, 0]);
        Assert.Equal(TestImages.Red, output[5, 5]);
    }

    [Fact]
    public async Task ComposeAsync_WithAnOpaqueSolidColorLayer_FillsTheRectangle()
    {
        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.X = 2;
        rectangle.Y = 2;
        rectangle.Width = 4;
        rectangle.Height = 4;
        rectangle.Color = "#0000FF";
        rectangle.ZIndex = 1;
        rectangle.Opacity = 1f;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(10, 10, TestImages.Red);
        request.Layers.Add(rectangle);

        CompositionResult result = await TestServices.CompositionService()
            .ComposeAsync(request, CancellationToken.None);

        using Image<Rgba32> output = TestImages.Decode(result.Png);

        Assert.Equal(TestImages.Blue, output[2, 2]);
        Assert.Equal(TestImages.Blue, output[5, 5]);
        Assert.Equal(TestImages.Red, output[1, 1]);
        Assert.Equal(TestImages.Red, output[6, 6]);
    }

    [Fact]
    public async Task ComposeAsync_WithAnImageLayerAndASolidColorLayer_DrawsBoth()
    {
        Layer overlay = new Layer();
        overlay.Type = LayerTypes.Image;
        overlay.X = 0;
        overlay.Y = 0;
        overlay.ZIndex = 1;
        overlay.Opacity = 1f;
        overlay.Image = TestImages.SolidPngStream(2, 2, TestImages.Green);

        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.X = 6;
        rectangle.Y = 6;
        rectangle.Width = 3;
        rectangle.Height = 3;
        rectangle.Color = "#0000FF";
        rectangle.ZIndex = 2;
        rectangle.Opacity = 1f;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(10, 10, TestImages.Red);
        request.Layers.Add(overlay);
        request.Layers.Add(rectangle);

        CompositionResult result = await TestServices.CompositionService()
            .ComposeAsync(request, CancellationToken.None);

        using Image<Rgba32> output = TestImages.Decode(result.Png);

        Assert.Equal(TestImages.Green, output[0, 0]);
        Assert.Equal(TestImages.Blue, output[6, 6]);
        Assert.Equal(TestImages.Red, output[4, 4]);
    }

    [Fact]
    public async Task ComposeAsync_WithAHalfTransparentLayer_BlendsWithTheBaseImage()
    {
        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.X = 0;
        rectangle.Y = 0;
        rectangle.Width = 10;
        rectangle.Height = 10;
        rectangle.Color = "#0000FF";
        rectangle.ZIndex = 1;
        rectangle.Opacity = 0.5f;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(10, 10, TestImages.Red);
        request.Layers.Add(rectangle);

        CompositionResult result = await TestServices.CompositionService()
            .ComposeAsync(request, CancellationToken.None);

        using Image<Rgba32> output = TestImages.Decode(result.Png);
        Rgba32 blended = output[5, 5];

        Assert.InRange(blended.R, 100, 155);
        Assert.InRange(blended.B, 100, 155);
        Assert.Equal(0, blended.G);
        Assert.Equal(255, blended.A);
    }

    [Fact]
    public async Task ComposeAsync_DrawsHigherZIndexLayersOnTop()
    {
        Layer bottom = new Layer();
        bottom.Type = LayerTypes.Solid;
        bottom.X = 0;
        bottom.Y = 0;
        bottom.Width = 8;
        bottom.Height = 8;
        bottom.Color = "#00FF00";
        bottom.ZIndex = 1;
        bottom.Opacity = 1f;

        Layer top = new Layer();
        top.Type = LayerTypes.Solid;
        top.X = 0;
        top.Y = 0;
        top.Width = 8;
        top.Height = 8;
        top.Color = "#0000FF";
        top.ZIndex = 5;
        top.Opacity = 1f;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(10, 10, TestImages.Red);

        // Submitted in the opposite order on purpose: the zIndex decides, not the request order.
        request.Layers.Add(top);
        request.Layers.Add(bottom);

        CompositionResult result = await TestServices.CompositionService()
            .ComposeAsync(request, CancellationToken.None);

        using Image<Rgba32> output = TestImages.Decode(result.Png);
        Assert.Equal(TestImages.Blue, output[1, 1]);
    }
}
