namespace ImageStamp.Core.Models;

/// <summary>
/// Everything the composition engine needs in order to produce the final PNG.
/// </summary>
public sealed class CompositionRequest
{
    public CompositionRequest()
    {
        BaseImage = Stream.Null;
        Layers = new List<Layer>();
    }

    /// <summary>
    /// PNG payload of the background image.
    /// </summary>
    public Stream BaseImage { get; set; }

    /// <summary>
    /// Original upload file name of the base image.
    /// </summary>
    public string? BaseImageFileName { get; set; }

    /// <summary>
    /// Layers to draw on top of the base image.
    /// </summary>
    public List<Layer> Layers { get; set; }
}
