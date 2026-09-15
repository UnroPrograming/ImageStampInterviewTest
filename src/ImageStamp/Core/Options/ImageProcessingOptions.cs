namespace ImageStamp.Core.Options;

/// <summary>
/// Tunables for the composition pipeline. Bound from the <c>ImageProcessing</c> configuration section.
/// </summary>
public sealed class ImageProcessingOptions
{
    public const string SectionName = "ImageProcessing";

    /// <summary>
    /// Largest accepted upload, in bytes, for the base image and for every image layer.
    /// </summary>
    public long MaxUploadBytes { get; set; } = 20L * 1024 * 1024;

    /// <summary>
    /// Largest accepted number of layers in a single composition.
    /// </summary>
    public int MaxLayers { get; set; } = 32;

    /// <summary>
    /// Size of the buffer used while reading uploaded payloads.
    /// </summary>
    public int CopyBufferSize { get; set; } = 81920;

    /// <summary>
    /// Deflate level used when encoding the resulting PNG, between 1 and 9.
    /// </summary>
    public int PngCompressionLevel { get; set; } = 6;

    /// <summary>
    /// Content types accepted for uploaded images.
    /// </summary>
    public string[] AllowedContentTypes { get; set; } = new[] { "image/png" };
}
