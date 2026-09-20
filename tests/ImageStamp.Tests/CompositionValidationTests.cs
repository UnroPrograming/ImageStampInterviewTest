using ImageStamp.Core.Models;
using ImageStamp.Core.Options;
using ImageStamp.Core.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ImageStamp.Tests;

public sealed class CompositionValidationTests
{
    [Fact]
    public void TryValidate_WithoutABaseImage_Fails()
    {
        CompositionRequest request = new CompositionRequest();

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("base image", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithAValidComposition_Succeeds()
    {
        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.Width = 10;
        rectangle.Height = 10;
        rectangle.Color = "#FF0000";
        rectangle.Opacity = 0.5f;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(rectangle);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.True(valid, error);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void TryValidate_WithAnOpacityOutsideTheAllowedRange_Fails(float opacity)
    {
        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.Width = 10;
        rectangle.Height = 10;
        rectangle.Color = "#FF0000";
        rectangle.Opacity = opacity;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(rectangle);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("opacity", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithASolidLayerWithoutAColour_Fails()
    {
        Layer rectangle = new Layer();
        rectangle.Type = LayerTypes.Solid;
        rectangle.Width = 10;
        rectangle.Height = 10;
        rectangle.Color = null;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(rectangle);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("colour", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithAnImageLayerWithoutAPayload_Fails()
    {
        Layer overlay = new Layer();
        overlay.Type = LayerTypes.Image;
        overlay.Image = null;

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(overlay);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("PNG payload", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithAnUnknownLayerType_Fails()
    {
        Layer unknown = new Layer();
        unknown.Type = "pixelate";  // ← Cambiamos "blur" a algo que NO existe

        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(4, 4, TestImages.Red);
        request.Layers.Add(unknown);

        bool valid = CompositionValidation.TryValidate(request, new ImageProcessingOptions(), out string error);

        Assert.False(valid);
        Assert.Contains("unsupported", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifica que una capa de blur se aplica sin errores y produce una imagen del tamaño correcto.
    /// Este test valida el caso más simple: blur sobre un color sólido uniforme.
    /// </summary>
    [Fact]
    public async Task ComposeAsync_WithABlurLayer_BlursTheSpecifiedRegion()
    {
        // Arrange: Crea una capa de blur que cubre toda la imagen (10x10)
        Layer blur = new Layer();
        blur.Type = LayerTypes.Blur;
        blur.X = 0;
        blur.Y = 0;
        blur.Width = 10;
        blur.Height = 10;
        blur.Sigma = 2;  // Intensidad del desenfoque
        blur.ZIndex = 1;

        // Arrange: Crea una solicitud de composición con imagen base roja y el blur
        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(10, 10, TestImages.Red);
        request.Layers.Add(blur);

        // Act: Ejecuta la composición
        CompositionResult result = await TestServices.CompositionService()
            .ComposeAsync(request, CancellationToken.None);

        // Assert: Verifica que la composición se completó sin excepciones
        // y que el tamaño de la imagen resultante es correcto
        using Image<Rgba32> output = TestImages.Decode(result.Png);
        Assert.Equal(10, result.Width);
        Assert.Equal(10, result.Height);
    }

    /// <summary>
    /// Verifica que una capa blur se aplica a las capas inferiores y que las capas superiores
    /// se dibujan intactas (nítidas) sobre el área borroneada.
    /// </summary>
    [Fact]
    public async Task ComposeAsync_WithBlurAndImageLayer_BlursUnderlyingAndDrawsImageOnTop()
    {
        // Arrange: Crea una capa de blur que cubre el área (0,0) a (8,8)
        Layer blur = new Layer();
        blur.Type = LayerTypes.Blur;
        blur.X = 0;
        blur.Y = 0;
        blur.Width = 8;
        blur.Height = 8;
        blur.Sigma = 2;
        blur.ZIndex = 1;  // Se aplica primero (zIndex más bajo)

        // Arrange: Crea una capa de imagen azul que se dibuja ENCIMA del blur
        Layer image = new Layer();
        image.Type = LayerTypes.Image;
        image.X = 8;
        image.Y = 8;
        image.ZIndex = 2;  // Se dibuja después (zIndex más alto)
        image.Opacity = 1f;
        image.Image = TestImages.SolidPngStream(2, 2, TestImages.Blue);

        // Arrange: Crea la solicitud con imagen base roja, blur y la imagen azul
        CompositionRequest request = new CompositionRequest();
        request.BaseImage = TestImages.SolidPngStream(10, 10, TestImages.Red);
        request.Layers.Add(blur);
        request.Layers.Add(image);

        // Act: Ejecuta la composición
        CompositionResult result = await TestServices.CompositionService()
            .ComposeAsync(request, CancellationToken.None);

        // Assert: Verifica que la imagen azul está intacta en su posición (8,8)
        // La imagen azul debe estar nítida aunque el blur esté debajo de ella
        using Image<Rgba32> output = TestImages.Decode(result.Png);
        Assert.Equal(TestImages.Blue, output[8, 8]);
        Assert.Equal(TestImages.Blue, output[9, 9]);
    }

    /// <summary>
    /// Verifica que el blur realmente afecta los píxeles dentro de la región especificada.
    /// Compara una composición sin blur con otra con blur para demostrar que hay cambios visibles.
    /// </summary>
    [Fact]
    public async Task ComposeAsync_WithABlurLayer_ActuallyBlursPixelsInRegion()
    {
        // Arrange: Crea una imagen base con contraste (cuadrado azul en fondo rojo)
        // para que el blur sea visible cuando suavice los bordes
        using Image<Rgba32> baseImage = new Image<Rgba32>(10, 10, TestImages.Red);
        // Dibuja un cuadrado azul en el centro (píxeles 3-7 en X e Y)
        for (int x = 3; x < 8; x++)
        {
            for (int y = 3; y < 8; y++)
            {
                baseImage[x, y] = TestImages.Blue;
            }
        }
        using MemoryStream baseStream = new MemoryStream();
        baseImage.SaveAsPng(baseStream);
        byte[] baseImageBytes = baseStream.ToArray();

        // Act: Composición SIN blur - obtiene el color original del píxel central
        CompositionRequest requestWithoutBlur = new CompositionRequest();
        requestWithoutBlur.BaseImage = new MemoryStream(baseImageBytes, writable: false);

        CompositionResult resultWithoutBlur = await TestServices.CompositionService()
            .ComposeAsync(requestWithoutBlur, CancellationToken.None);

        using Image<Rgba32> outputWithoutBlur = TestImages.Decode(resultWithoutBlur.Png);
        Rgba32 pixelWithoutBlur = outputWithoutBlur[5, 5];  // Centro del cuadrado azul

        // Act: Composición CON blur en la región que contiene los bordes azul-rojo
        Layer blur = new Layer();
        blur.Type = LayerTypes.Blur;
        blur.X = 2;
        blur.Y = 2;
        blur.Width = 6;
        blur.Height = 6;
        blur.Sigma = 2;
        blur.ZIndex = 1;

        CompositionRequest requestWithBlur = new CompositionRequest();
        requestWithBlur.BaseImage = new MemoryStream(baseImageBytes, writable: false);
        requestWithBlur.Layers.Add(blur);

        CompositionResult resultWithBlur = await TestServices.CompositionService()
            .ComposeAsync(requestWithBlur, CancellationToken.None);

        using Image<Rgba32> outputWithBlur = TestImages.Decode(resultWithBlur.Png);
        Rgba32 pixelWithBlur = outputWithBlur[5, 5];

        // Assert: El píxel debe cambiar debido al blur que suaviza los bordes
        // El blur mezcla los píxeles vecinos, alterando el color en la región de transición
        Assert.NotEqual(pixelWithoutBlur, pixelWithBlur);
    }
}
