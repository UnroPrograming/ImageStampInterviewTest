using ImageStamp.Core.Models;
using ImageStamp.Core.Options;

using SixLabors.ImageSharp;

namespace ImageStamp.Core.Validation;

/// <summary>
/// Input checks applied before a composition reaches the image pipeline.
/// </summary>
public static class CompositionValidation
{
    public static bool TryValidate(CompositionRequest request, ImageProcessingOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        if (request.BaseImage == Stream.Null)
        {
            error = "A base image is required.";
            return false;
        }

        if (request.Layers.Count > options.MaxLayers)
        {
            error = "A composition cannot have more than " + options.MaxLayers + " layers.";
            return false;
        }

        for (int i = 0; i < request.Layers.Count; i++)
        {
            Layer layer = request.Layers[i];

            if (!TryValidateLayer(layer, i, out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateLayer(Layer layer, int index, out string error)
    {
        if (layer.Opacity < 0f || layer.Opacity > 1f)
        {
            error = "Layer " + index + ": opacity must be between 0 and 1.";
            return false;
        }

        if (layer.Type == LayerTypes.Image)
        {
            if (layer.Image is null)
            {
                error = "Layer " + index + ": an image layer requires a PNG payload.";
                return false;
            }
        }
        else if (layer.Type == LayerTypes.Solid)
        {
            if (layer.Width is null || layer.Height is null)
            {
                error = "Layer " + index + ": a solid colour layer requires a width and a height.";
                return false;
            }

            if (layer.Width.Value <= 0 || layer.Height.Value <= 0)
            {
                error = "Layer " + index + ": width and height must be greater than zero.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(layer.Color) || !Color.TryParseHex(layer.Color, out _))
            {
                error = "Layer " + index + ": a valid hex colour is required, for example #FF0000.";
                return false;
            }
        }
        else
        {
            error = "Layer " + index + ": unsupported layer type '" + layer.Type + "'.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
