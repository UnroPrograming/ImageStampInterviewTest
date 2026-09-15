namespace ImageStamp.Core.Models;

/// <summary>
/// Outcome of a successful composition.
/// </summary>
public sealed class CompositionResult
{
    public CompositionResult(byte[] png, int width, int height, long processingTimeMs)
    {
        Png = png;
        Width = width;
        Height = height;
        ProcessingTimeMs = processingTimeMs;
    }

    /// <summary>
    /// Encoded PNG of the final composition.
    /// </summary>
    public byte[] Png { get; }

    public int Width { get; }

    public int Height { get; }

    public long ProcessingTimeMs { get; }

    public long OutputSizeBytes
    {
        get { return Png.LongLength; }
    }
}
