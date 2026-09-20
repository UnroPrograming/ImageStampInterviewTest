namespace ImageStamp.Core.Models;

/// <summary>
/// Everything the composition engine needs in order to produce the final PNG.
/// </summary>
public sealed class CompositionRequest
{
    public CompositionRequest()
    {
        BaseImage = new BaseImageInput { Content = Stream.Null };
        Layers = new List<Layer>();
    }

    /// <summary>
    /// Background image: its PNG payload and original upload file name.
    /// </summary>
    public BaseImageInput BaseImage { get; set; }

    /// <summary>
    /// Layers to draw on top of the base image.
    /// </summary>
    public List<Layer> Layers { get; set; }
}