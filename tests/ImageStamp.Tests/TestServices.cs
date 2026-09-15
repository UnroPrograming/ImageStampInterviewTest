using ImageStamp.Core.Imaging;
using ImageStamp.Core.Options;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ImageStamp.Tests;

internal static class TestServices
{
    public static ImageProcessingOptions DefaultOptions()
    {
        return new ImageProcessingOptions();
    }

    public static ImageCompositionService CompositionService(ImageProcessingOptions? options = null)
    {
        return new ImageCompositionService(
            Options.Create(options ?? DefaultOptions()),
            NullLogger<ImageCompositionService>.Instance);
    }
}
