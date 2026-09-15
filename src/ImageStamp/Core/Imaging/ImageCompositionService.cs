using System.Diagnostics;

using ImageStamp.Core.Models;
using ImageStamp.Core.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageStamp.Core.Imaging;

/// <summary>
/// Draws the layers of a composition on top of its base image and encodes the result as PNG.
/// </summary>
public sealed class ImageCompositionService
{
    private readonly ImageProcessingOptions _options;
    private readonly ILogger<ImageCompositionService> _logger;
    private readonly PngEncoder _encoder;
    private readonly byte[] _copyBuffer;
    private readonly List<RenderableLayer> _renderQueue;

    private long _lastOutputSizeBytes;

    public ImageCompositionService(IOptions<ImageProcessingOptions> options, ILogger<ImageCompositionService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        _encoder = new PngEncoder
        {
            ColorType = PngColorType.RgbWithAlpha,
            BitDepth = PngBitDepth.Bit8,
            CompressionLevel = (PngCompressionLevel)Math.Clamp(_options.PngCompressionLevel, 0, 9),
        };

        // The encoder, the copy buffer and the render queue are built once and reused for every
        // composition: allocating an 80 KB array and a fresh list for each layer of each request ends
        // up on the large object heap and shows as gen2 pressure as soon as traffic grows.
        _copyBuffer = new byte[Math.Max(4096, _options.CopyBufferSize)];
        _renderQueue = new List<RenderableLayer>(_options.MaxLayers);
    }

    /// <summary>
    /// Size in bytes of the last PNG produced by this service. Used for lightweight diagnostics.
    /// </summary>
    public long LastOutputSizeBytes
    {
        get { return _lastOutputSizeBytes; }
    }

    /// <summary>
    /// Composes the base image and its layers into a single PNG.
    /// </summary>
    public async Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Stopwatch stopwatch = Stopwatch.StartNew();

        byte[] baseImageBytes = await BufferAsync(request.BaseImage, cancellationToken).ConfigureAwait(false);

        _renderQueue.Clear();

        try
        {
            foreach (Layer layer in request.Layers)
            {
                _renderQueue.Add(await CreateRenderableAsync(layer, cancellationToken).ConfigureAwait(false));
            }

            using Image<Rgba32> canvas = Image.Load<Rgba32>(baseImageBytes);

            await Task.Run(() => Compose(canvas, _renderQueue)).ConfigureAwait(false);

            using MemoryStream output = new MemoryStream();
            await canvas.SaveAsync(output, _encoder, cancellationToken).ConfigureAwait(false);

            byte[] png = output.ToArray();

            stopwatch.Stop();
            _lastOutputSizeBytes = png.LongLength;

            _logger.LogInformation(
                "Composed a {Width}x{Height} image from {LayerCount} layers in {ElapsedMilliseconds} ms ({OutputBytes} bytes).",
                canvas.Width,
                canvas.Height,
                _renderQueue.Count,
                stopwatch.ElapsedMilliseconds,
                png.LongLength);

            return new CompositionResult(png, canvas.Width, canvas.Height, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            foreach (RenderableLayer renderable in _renderQueue)
            {
                renderable.Dispose();
            }
        }
    }

    private static void Compose(Image<Rgba32> canvas, List<RenderableLayer> renderables)
    {
        renderables.Sort(CompareByZIndex);

        foreach (RenderableLayer renderable in renderables)
        {
            Layer layer = renderable.Source;
            Image<Rgba32> content = renderable.Content;
            Point origin = new Point(layer.X, layer.Y);

            canvas.Mutate(context => context.DrawImage(content, origin, layer.Opacity));
        }
    }

    private static int CompareByZIndex(RenderableLayer left, RenderableLayer right)
    {
        return left.Source.ZIndex - right.Source.ZIndex;
    }

    private async Task<RenderableLayer> CreateRenderableAsync(Layer layer, CancellationToken cancellationToken)
    {
        if (layer.Type == LayerTypes.Image)
        {
            if (layer.Image is null)
            {
                throw new InvalidOperationException("An image layer was submitted without a PNG payload.");
            }

            byte[] content = await BufferAsync(layer.Image, cancellationToken).ConfigureAwait(false);
            return new RenderableLayer(layer, Image.Load<Rgba32>(content));
        }
        else if (layer.Type == LayerTypes.Solid)
        {
            Rgba32 color = Color.ParseHex(layer.Color!).ToPixel<Rgba32>();
            Image<Rgba32> rectangle = new Image<Rgba32>(layer.Width!.Value, layer.Height!.Value, color);
            return new RenderableLayer(layer, rectangle);
        }
        else
        {
            throw new NotSupportedException("Layer type '" + layer.Type + "' is not supported.");
        }
    }

    private async Task<byte[]> BufferAsync(Stream source, CancellationToken cancellationToken)
    {
        using MemoryStream target = new MemoryStream();

        int read;
        while ((read = await source.ReadAsync(_copyBuffer, 0, _copyBuffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            target.Write(_copyBuffer, 0, read);
        }

        return target.ToArray();
    }

    private sealed class RenderableLayer(Layer source, Image<Rgba32> content) : IDisposable
    {
        public Layer Source { get; } = source;

        public Image<Rgba32> Content { get; } = content;

        public void Dispose()
        {
            Content.Dispose();
        }
    }
}
