namespace ImageStamp.Api.Contracts;

/// <summary>
/// Shape of a single entry of the <c>layers</c> field of a composition request.
/// 
/// Three layer types are supported:
/// 
/// 1. IMAGE: Draws a PNG image at the specified position
///    Required fields: Type, X, Y, ZIndex, Opacity, ImageKey
///    Example: {"type":"image","x":10,"y":10,"opacity":1.0,"zIndex":1,"imageKey":"logo"}
/// 
/// 2. SOLID: Draws a solid-color rectangle
///    Required fields: Type, X, Y, Width, Height, Color, Opacity, ZIndex
///    Example: {"type":"solid","x":100,"y":50,"width":300,"height":100,"color":"#FF0000","opacity":0.5,"zIndex":3}
/// 
/// 3. BLUR: Blurs a rectangular region using GaussianBlur
///    Required fields: Type, X, Y, Width, Height, Sigma, ZIndex
///    Example: {"type":"blur","x":0,"y":120,"width":600,"height":180,"sigma":8,"zIndex":2}
/// </summary>
public sealed class LayerRequest
{
    public LayerRequest()
    {
        Opacity = 1f;
    }

    /// <summary>
    /// Layer type discriminator. Allowed values: "image", "solid", "blur"
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Horizontal position in pixels from the top-left corner.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Vertical position in pixels from the top-left corner.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Stacking order: higher values are drawn on top.
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// Opacity value between 0 (invisible) and 1 (fully opaque).
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    /// [IMAGE] Name of the multipart form field containing the PNG file.
    /// </summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// [SOLID, BLUR] Width of the rectangle in pixels.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// [SOLID, BLUR] Height of the rectangle in pixels.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// [SOLID] Hexadecimal color (example: #FF0000 for red).
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// [BLUR] Blur intensity (typical values: 2-15).
    /// </summary>
    public float? Sigma { get; set; }
}