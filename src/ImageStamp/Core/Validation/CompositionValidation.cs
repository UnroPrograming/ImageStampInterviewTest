using ImageStamp.Core.Models;
using ImageStamp.Core.Options;

using SixLabors.ImageSharp;

namespace ImageStamp.Core.Validation;

/// <summary>
/// Input checks applied before a composition reaches the image pipeline.
/// </summary>
public static class CompositionValidation
{
    /// <summary>
    /// Valida un request de composición simple.
    /// </summary>
    public static bool TryValidate(CompositionRequest request, ImageProcessingOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        if (request.BaseImage == Stream.Null)
        {
            error = "A base image is required.";
            return false;
        }

        return TryValidateLayers(request.Layers, options, out error);
    }

    /// <summary>
    /// Valida un request de composición por lotes.
    /// </summary>
    public static bool TryValidate(BatchCompositionRequest request, ImageProcessingOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        if (request.BaseImages.Count == 0)
        {
            error = "At least one base image is required.";
            return false;
        }

        foreach (BaseImageInput baseImage in request.BaseImages)
        {
            if (baseImage.Content == Stream.Null)
            {
                error = "A base image is required.";
                return false;
            }
        }

        return TryValidateLayers(request.Layers, options, out error);
    }

    /// <summary>
    /// Valida solo la lista de capas (usado internamente por ambas sobrecargas).
    /// </summary>
    private static bool TryValidateLayers(List<Layer> layers, ImageProcessingOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(options);

        if (layers.Count > options.MaxLayers)
        {
            error = "A composition cannot have more than " + options.MaxLayers + " layers.";
            return false;
        }

        for (int i = 0; i < layers.Count; i++)
        {
            Layer layer = layers[i];

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
        // Validate opacity (common to all layer types)
        if (layer.Opacity < 0f || layer.Opacity > 1f)
        {
            error = "Layer " + index + ": opacity must be between 0 and 1.";
            return false;
        }

        if (layer.Type == LayerTypes.Image)
        {
            return TryValidateImageLayer(layer, index, out error);
        }
        else if (layer.Type == LayerTypes.Solid)
        {
            return TryValidateSolidLayer(layer, index, out error);
        }
        else if (layer.Type == LayerTypes.Blur)
        {
            return TryValidateBlurLayer(layer, index, out error);
        }
        else
        {
            error = "Layer " + index + ": unsupported layer type '" + layer.Type + "'.";
            return false;
        }
    }

    private static bool TryValidateImageLayer(Layer layer, int index, out string error)
    {
        if (layer.Image is null)
        {
            error = "Layer " + index + ": an image layer requires a PNG payload.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateSolidLayer(Layer layer, int index, out string error)
    {
        // Width and height are required for solid layers
        if (layer.Width is null || layer.Height is null)
        {
            error = "Layer " + index + ": a solid colour layer requires a width and a height.";
            return false;
        }

        // Width and height must be positive
        if (layer.Width.Value <= 0 || layer.Height.Value <= 0)
        {
            error = "Layer " + index + ": width and height must be greater than zero.";
            return false;
        }

        // Color is required and must be a valid hex value
        if (string.IsNullOrWhiteSpace(layer.Color) || !Color.TryParseHex(layer.Color, out _))
        {
            error = "Layer " + index + ": a valid hex colour is required, for example #FF0000.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateBlurLayer(Layer layer, int index, out string error)
    {
        // Width and height are required for blur layers
        if (layer.Width is null || layer.Height is null)
        {
            error = "Layer " + index + ": a blur layer requires a width and a height.";
            return false;
        }

        // Width and height must be positive
        if (layer.Width.Value <= 0 || layer.Height.Value <= 0)
        {
            error = "Layer " + index + ": blur region width and height must be greater than zero.";
            return false;
        }

        // Sigma is required for blur effect
        if (layer.Sigma is null)
        {
            error = "Layer " + index + ": a blur layer requires a sigma (blur intensity) value.";
            return false;
        }

        // Sigma must be positive
        if (layer.Sigma.Value <= 0)
        {
            error = "Layer " + index + ": sigma must be greater than zero.";
            return false;
        }

        // Sigma should be within reasonable bounds (prevent excessive blur)
        if (layer.Sigma.Value > 50)
        {
            error = "Layer " + index + ": sigma must be 50 or less (excessive blur is not allowed).";
            return false;
        }

        error = string.Empty;
        return true;
    }
}