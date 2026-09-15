namespace ImageStamp.Api.Contracts;

/// <summary>
/// Shape of a single entry of the <c>layers</c> field of a composition request.
/// </summary>
public sealed class LayerRequest
{
    public LayerRequest()
    {
        Opacity = 1f;
    }

    /// <summary>
    /// Either <c>image</c> or <c>solid</c>.
    /// </summary>
    public string? Type { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int ZIndex { get; set; }

    public float Opacity { get; set; }

    /// <summary>
    /// Name of the multipart file that carries the PNG of an image layer.
    /// </summary>
    public string? ImageKey { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? Color { get; set; }
}
