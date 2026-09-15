using ImageStamp.Core.Models;
using ImageStamp.Core.Options;
using ImageStamp.Core.Validation;

using Xunit;

namespace ImageStamp.Tests;

public sealed class CompositionValidationTests
{
    [Fact]
    public void TryValidate_WithoutABaseImage_Fails()
    {
        CompositionRequest request = new CompositionRequest();

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("base image", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithAValidComposition_Succeeds()
    {
        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.Width = 10;
        rectangle.Height = 10;
        rectangle.Color = "#FF0000";
        rectangle.Opacity = 0.5f;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(rectangle);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.True(valid, error);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void TryValidate_WithAnOpacityOutsideTheAllowedRange_Fails(float opacity)
    {
        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.Width = 10;
        rectangle.Height = 10;
        rectangle.Color = "#FF0000";
        rectangle.Opacity = opacity;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(rectangle);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("opacity", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithASolidLayerWithoutAColour_Fails()
    {
        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.Width = 10;
        rectangle.Height = 10;
        rectangle.Color = null;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(rectangle);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("colour", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithAnImageLayerWithoutAPayload_Fails()
    {
        Layer overlay = new Layer();
        overlay.Type = LayerTypes.Image;
        overlay.Image = null;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(overlay);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("PNG payload", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithAnUnknownLayerType_Fails()
    {
        Layer unknown = new Layer();
        unknown.Type = "blur";

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(unknown);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("unsupported", error, StringComparison.OrdinalIgnoreCase);
    }
}
