using ImageStamp.Core.Mapping;
using ImageStamp.Core.Models;
using ImageStamp.Core.Options;

using Microsoft.AspNetCore.Http;

using Xunit;

namespace ImageStamp.Tests;

public sealed class LayerMapperTests
{
    /// <summary>
    /// Regresión: LayerMapper debía usar Layer.SetImageContent (que cachea los bytes) en vez de
    /// asignar Layer.Image directamente. Con la asignación directa, Layer.Clone() no tenía bytes
    /// que reutilizar y compartía el mismo Stream entre clones — el segundo Clone() leía un
    /// stream ya agotado. Este test reproduce justo ese camino: parsear una capa de imagen con
    /// LayerMapper y luego clonarla dos veces, como hace el endpoint /batch para cada imagen base.
    /// </summary>
    [Fact]
    public async Task ParseLayersAsync_ThenClonedTwice_BothClonesCanReadTheirOwnImageContent()
    {
        // Arrange: un form con una capa de tipo "image" referenciando el fichero "logo"
        byte[] logoBytes = TestImages.SolidPng(2, 2, TestImages.Blue);

        FormFileCollection files = new FormFileCollection
        {
            new FormFile(new MemoryStream(logoBytes), 0, logoBytes.Length, "logo", "logo.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png",
            },
        };

        string layersJson = """
            [{"type":"image","x":0,"y":0,"opacity":1.0,"zIndex":1,"imageKey":"logo"}]
            """;

        IFormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);

        // Act: parsea las capas, tal como hace el controller antes del bucle del batch
        (List<Layer> layers, string? error) = await LayerMapper.ParseLayersAsync(
            layersJson, form, new ImageProcessingOptions(), CancellationToken.None);

        Assert.Null(error);
        Layer imageLayer = Assert.Single(layers);

        // Act: clona la misma capa dos veces, como hace Batch() para dos imágenes base distintas
        Layer clone1 = imageLayer.Clone();
        Layer clone2 = imageLayer.Clone();

        // Assert: ambos clones deben poder leer su contenido de imagen completo de forma
        // independiente. Antes del fix, el segundo Read devolvía 0 bytes porque ambos clones
        // compartían el mismo Stream ya agotado por el primero.
        byte[] contentFromClone1 = await ReadAllAsync(clone1.Image!);
        byte[] contentFromClone2 = await ReadAllAsync(clone2.Image!);

        Assert.Equal(logoBytes, contentFromClone1);
        Assert.Equal(logoBytes, contentFromClone2);
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using MemoryStream buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}