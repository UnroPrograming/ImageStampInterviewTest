namespace ImageStamp.Core.Models;

/// <summary>
/// Well-known discriminator values for <see cref="Layer.Type"/>.
/// Determines which properties are used and how the layer is rendered.
/// </summary>
public static class LayerTypes
{
    /// <summary>
    /// Image layer: renders a PNG file at the specified position.
    /// Uses: <see cref="Layer.Image"/>, <see cref="Layer.FileName"/>, <see cref="Layer.X"/>, <see cref="Layer.Y"/>, <see cref="Layer.Opacity"/>, <see cref="Layer.ZIndex"/>
    /// </summary>
    public const string Image = "image";

    /// <summary>
    /// Solid layer: renders a solid-color rectangle.
    /// Uses: <see cref="Layer.Color"/>, <see cref="Layer.Width"/>, <see cref="Layer.Height"/>, <see cref="Layer.X"/>, <see cref="Layer.Y"/>, <see cref="Layer.Opacity"/>, <see cref="Layer.ZIndex"/>
    /// </summary>
    public const string Solid = "solid";

    /// <summary>
    /// Blur layer: applies a Gaussian blur to a rectangular region.
    /// Uses: <see cref="Layer.Sigma"/>, <see cref="Layer.Width"/>, <see cref="Layer.Height"/>, <see cref="Layer.X"/>, <see cref="Layer.Y"/>, <see cref="Layer.ZIndex"/>
    /// </summary>
    public const string Blur = "blur";
}

/// <summary>
/// Represents a single renderable element of a composition, drawn on top of the base image.
/// </summary>
/// <remarks>
/// The service uses a discriminator pattern: the <see cref="Type"/> field determines which properties
/// are meaningful and how the layer is rendered.
/// 
/// Design note: The service originally supported only image overlays. Solid-color rectangles were added
/// later using the same Layer shape, allowing new layer types to be added without changing the HTTP contract
/// or persistence schema.
/// </remarks>
public sealed class Layer
{
    public Layer()
    {
        Type = LayerTypes.Image;
        Opacity = 1f;
    }

    /// <summary>
    /// Layer type discriminator. See <see cref="LayerTypes"/> for allowed values and their semantics.
    /// Determines which other properties are used: Image, Solid, or Blur.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Horizontal position in pixels, measured from the left edge of the base image.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Vertical position in pixels, measured from the top edge of the base image.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Stacking order: determines draw order relative to other layers.
    /// Higher values are drawn on top. Comparable to z-index in CSS.
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// Opacity (transparency) applied during rendering: 0 (fully transparent) to 1 (fully opaque).
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    /// [IMAGE] PNG file stream. Required and only used when <see cref="Type"/> is <see cref="LayerTypes.Image"/>.
    /// </summary>
    public Stream? Image { get; set; }

    /// <summary>
    /// Original file name of the uploaded image, stored in metadata for audit/reference purposes.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// [SOLID, BLUR] Width in pixels of the rectangle.
    /// Required when <see cref="Type"/> is <see cref="LayerTypes.Solid"/> or <see cref="LayerTypes.Blur"/>.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// [SOLID, BLUR] Height in pixels of the rectangle.
    /// Required when <see cref="Type"/> is <see cref="LayerTypes.Solid"/> or <see cref="LayerTypes.Blur"/>.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// [SOLID] Hexadecimal color code (e.g., #FF0000 for red, #00FF00 for green).
    /// Required when <see cref="Type"/> is <see cref="LayerTypes.Solid"/>.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// [BLUR] Gaussian blur intensity (standard deviation).
    /// Typical values range from 2 to 15. Higher values produce stronger blur effects.
    /// Required when <see cref="Type"/> is <see cref="LayerTypes.Blur"/>.
    /// </summary>
    public float? Sigma { get; set; }
}