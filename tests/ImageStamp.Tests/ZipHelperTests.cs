using System.IO.Compression;

using ImageStamp.Core.Imaging;

using Xunit;

namespace ImageStamp.Tests;

public sealed class ZipHelperTests
{
    /// <summary>
    /// Verifica que cada imagen de la lista se convierte en una entrada independiente
    /// dentro del ZIP, con el nombre + extensión .png esperados.
    /// </summary>
    [Fact]
    public void CreateZipFromImages_WithMultipleImages_CreatesOneEntryPerImage()
    {
        // Arrange: tres imágenes distintas, cada una con su nombre lógico (sin extensión)
        List<(string fileName, byte[] png)> images = new List<(string, byte[])>
        {
            ("photo1", TestImages.SolidPng(4, 4, TestImages.Red)),
            ("photo2", TestImages.SolidPng(4, 4, TestImages.Blue)),
            ("photo3", TestImages.SolidPng(4, 4, TestImages.Green)),
        };

        // Act: crea el ZIP
        byte[] zipBytes = ZipHelper.CreateZipFromImages(images);

        // Assert: abre el ZIP resultante y comprueba que tiene 3 entradas,
        // una por cada imagen, con el nombre de archivo correcto (+.png)
        using MemoryStream zipStream = new MemoryStream(zipBytes);
        using ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        Assert.Equal(3, archive.Entries.Count);
        Assert.Contains(archive.Entries, e => e.FullName == "photo1.png");
        Assert.Contains(archive.Entries, e => e.FullName == "photo2.png");
        Assert.Contains(archive.Entries, e => e.FullName == "photo3.png");
    }

    /// <summary>
    /// Verifica que el contenido de cada entrada del ZIP es exactamente igual
    /// al PNG original, sin corrupción ni alteración durante el empaquetado.
    /// </summary>
    [Fact]
    public void CreateZipFromImages_EntryContent_MatchesOriginalBytes()
    {
        // Arrange: una sola imagen, guardamos sus bytes originales para comparar después
        byte[] originalPng = TestImages.SolidPng(4, 4, TestImages.Red);
        List<(string fileName, byte[] png)> images = new List<(string, byte[])>
        {
            ("photo1", originalPng),
        };

        // Act: crea el ZIP
        byte[] zipBytes = ZipHelper.CreateZipFromImages(images);

        // Assert: extrae la única entrada del ZIP y compara sus bytes
        // byte a byte con el PNG original
        using MemoryStream zipStream = new MemoryStream(zipBytes);
        using ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        ZipArchiveEntry entry = archive.Entries.Single();

        using MemoryStream extracted = new MemoryStream();
        using (Stream entryStream = entry.Open())
        {
            entryStream.CopyTo(extracted);
        }

        Assert.Equal(originalPng, extracted.ToArray());
    }

    /// <summary>
    /// Verifica que crear un ZIP sin ninguna imagen es un error explícito,
    /// en vez de devolver silenciosamente un ZIP vacío.
    /// </summary>
    [Fact]
    public void CreateZipFromImages_WithNoImages_Throws()
    {
        // Arrange: lista vacía
        List<(string fileName, byte[] png)> images = new List<(string, byte[])>();

        // Act & Assert: debe lanzar, no devolver un ZIP de 0 entradas
        Assert.Throws<InvalidOperationException>(() => ZipHelper.CreateZipFromImages(images));
    }
}