namespace ImageStamp.Core.Models;

/// <summary>
/// Well known values for <see cref="Layer.Type"/>.
/// </summary>
public static class LayerTypes
{
    public const string Image = "image";

    public const string Solid = "solid";
}

/// <summary>
/// A single element that is drawn on top of the base image of a composition.
/// </summary>
/// <remarks>
/// The service originally only supported overlaying PNG files. Solid colour rectangles were
/// added later on top of the same shape so that the HTTP contract and the persistence schema
/// did not have to change.
/// </remarks>
public sealed class Layer
{
    public Layer()
    {
        Type = LayerTypes.Image;
        Opacity = 1f;
    }

    /// <summary>
    /// Discriminator. See <see cref="LayerTypes"/>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Horizontal offset, in pixels, from the top left corner of the base image.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Vertical offset, in pixels, from the top left corner of the base image.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Stacking order of the layer within the composition.
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// Opacity applied when the layer is drawn, between 0 and 1.
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    /// PNG payload. Only meaningful for <see cref="LayerTypes.Image"/>.
    /// </summary>
    public Stream? Image { get; set; }

    /// <summary>
    /// Original upload file name, kept for the composition metadata.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Width of the rectangle. Only meaningful for <see cref="LayerTypes.Solid"/>.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the rectangle. Only meaningful for <see cref="LayerTypes.Solid"/>.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Hex colour, for example <c>#FF0000</c>. Only meaningful for <see cref="LayerTypes.Solid"/>.
    /// </summary>
    public string? Color { get; set; }
}
