using System.IO.Compression;

namespace ImageStamp.Core.Imaging;

/// <summary>
/// Helper para crear archivos ZIP con múltiples composiciones.
/// Usado por el endpoint batch para empaquetar todos los PNGs resultantes.
/// </summary>
public static class ZipHelper
{
    /// <summary>
    /// Crea un archivo ZIP conteniendo múltiples imágenes PNG.
    /// </summary>
    /// <param name="images">Lista de tuplas (nombreArchivo, bytesPNG).
    /// El nombre no debe incluir extensión: se agrega automáticamente .png</param>
    /// <returns>Bytes del archivo ZIP listo para descargar</returns>
    public static byte[] CreateZipFromImages(List<(string fileName, byte[] png)> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        if (images.Count == 0)
        {
            throw new InvalidOperationException("At least one image is required to create a ZIP file.");
        }

        using MemoryStream zipStream = new MemoryStream();
        using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string fileName, byte[] png) in images)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
                ArgumentNullException.ThrowIfNull(png);

                // Crea una entrada en el ZIP con el nombre + extensión .png
                ZipArchiveEntry entry = zip.CreateEntry($"{fileName}.png");

                // Escribe los bytes del PNG en la entrada
                using Stream entryStream = entry.Open();
                entryStream.Write(png, 0, png.Length);
            }
        }

        return zipStream.ToArray();
    }
}