using ImageStamp.Core.Models;
using ImageStamp.Core.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;

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

        // Carga la imagen base en memoria
        byte[] baseImageBytes = await BufferAsync(request.BaseImage, cancellationToken).ConfigureAwait(false);

        _renderQueue.Clear();

        try
        {
            // Prepara todas las capas para renderizado
            foreach (Layer layer in request.Layers)
            {
                _renderQueue.Add(await CreateRenderableAsync(layer, cancellationToken).ConfigureAwait(false));
            }

            // Carga la imagen base como lienzo
            using Image<Rgba32> canvas = Image.Load<Rgba32>(baseImageBytes);

            // Dibuja todas las capas en el lienzo
            await Task.Run(() => Compose(canvas, _renderQueue)).ConfigureAwait(false);

            // Codifica el resultado como PNG
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
            // Libera los recursos de todas las capas
            foreach (RenderableLayer renderable in _renderQueue)
            {
                renderable.Dispose();
            }
        }
    }

    /// <summary>
    /// Dibuja todas las capas en el lienzo, ordenadas por zIndex.
    /// </summary>
    private static void Compose(Image<Rgba32> canvas, List<RenderableLayer> renderables)
    {
        // Ordena las capas por zIndex (números menores primero = abajo)
        renderables.Sort(CompareByZIndex);

        foreach (RenderableLayer renderable in renderables)
        {
            Layer layer = renderable.Source;

            // Las capas de blur se aplican directamente al lienzo en su posición de zIndex
            // Por lo tanto se aplicará el Blur a las capas inferiores
            if (layer.Type == LayerTypes.Blur)
            {
                ApplyBlurToRegion(canvas, layer);
            }
            else
            {
                // Las capas de imagen y color sólido se dibujan como imágenes
                Image<Rgba32> content = renderable.Content;
                Point origin = new Point(layer.X, layer.Y);
                canvas.Mutate(context => context.DrawImage(content, origin, layer.Opacity));
            }
        }
    }

    /// <summary>
    /// Aplica un desenfoque Gaussiano a una región rectangular del lienzo.
    /// </summary>
    private static void ApplyBlurToRegion(Image<Rgba32> canvas, Layer blurLayer)
    {
        Rectangle blurRegion = new Rectangle(
            blurLayer.X,
            blurLayer.Y,
            blurLayer.Width!.Value,
            blurLayer.Height!.Value);

        // Ajusta la región a los límites del lienzo para evitar errores de índice fuera de rango
        Rectangle clampedRegion = Rectangle.Intersect(blurRegion, canvas.Bounds);

        // Si la región ajustada está fuera del lienzo, no hace nada
        if (clampedRegion.Width <= 0 || clampedRegion.Height <= 0)
        {
            return;
        }

        // Aplica desenfoque Gaussiano a la región especificada
        canvas.Mutate(context =>
        {
            context.Crop(clampedRegion)
                   .GaussianBlur(blurLayer.Sigma!.Value);
        });
    }

    /// <summary>
    /// Comparador para ordenar capas por zIndex en orden ascendente.
    /// </summary>
    private static int CompareByZIndex(RenderableLayer left, RenderableLayer right)
    {
        return left.Source.ZIndex - right.Source.ZIndex;
    }

    /// <summary>
    /// Convierte una capa en un contenido renderizable (imagen).
    /// </summary>
    private async Task<RenderableLayer> CreateRenderableAsync(Layer layer, CancellationToken cancellationToken)
    {
        if (layer.Type == LayerTypes.Image)
        {
            // Capa de imagen: carga el PNG
            if (layer.Image is null)
            {
                throw new InvalidOperationException("An image layer was submitted without a PNG payload.");
            }

            byte[] content = await BufferAsync(layer.Image, cancellationToken).ConfigureAwait(false);
            return new RenderableLayer(layer, Image.Load<Rgba32>(content));
        }
        else if (layer.Type == LayerTypes.Solid)
        {
            // Capa sólida: crea un rectángulo del color especificado
            Rgba32 color = Color.ParseHex(layer.Color!).ToPixel<Rgba32>();
            Image<Rgba32> rectangle = new Image<Rgba32>(layer.Width!.Value, layer.Height!.Value, color);
            return new RenderableLayer(layer, rectangle);
        }
        else if (layer.Type == LayerTypes.Blur)
        {
            // Capa de blur: no necesita contenido de imagen, se aplica directamente al lienzo
            // Retorna una imagen ficticia que se ignorará durante el renderizado
            Image<Rgba32> dummy = new Image<Rgba32>(1, 1, new Rgba32(0, 0, 0, 0));
            return new RenderableLayer(layer, dummy);
        }

        else
        {
            throw new NotSupportedException("Layer type '" + layer.Type + "' is not supported.");
        }
    }

    /// <summary>
    /// Lee un stream completamente en memoria.
    /// </summary>
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

    /// <summary>
    /// Representa una capa lista para renderizar: contiene la capa original y su contenido de imagen.
    /// </summary>
    private sealed class RenderableLayer(Layer source, Image<Rgba32> content) : IDisposable
    {
        /// <summary>
        /// La capa original con todos sus parámetros.
        /// </summary>
        public Layer Source { get; } = source;

        /// <summary>
        /// El contenido de la imagen ya cargado y listo para dibujar.
        /// </summary>
        public Image<Rgba32> Content { get; } = content;

        /// <summary>
        /// Libera los recursos de la imagen.
        /// </summary>
        public void Dispose()
        {
            Content.Dispose();
        }
    }
}