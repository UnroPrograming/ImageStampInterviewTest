namespace ImageStamp.Core.Imaging;

public static class StreamHelpers
{
    /// <summary>
    /// Reads a stream to the end and returns its content.
    /// </summary>
    /// <remarks>
    /// Uploaded streams are not always seekable and several stages of the pipeline need to look at
    /// the same payload more than once (validation, composition, metadata), so the content is
    /// materialised once here and reused from that point on.
    /// </remarks>
    public static async Task<byte[]> ReadFullyAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream is MemoryStream buffered)
        {
            return buffered.ToArray();
        }

        using MemoryStream target = new MemoryStream();
        await stream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        return target.ToArray();
    }
}
