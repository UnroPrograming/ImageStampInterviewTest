using ImageStamp.Core.Options;

using Microsoft.AspNetCore.Http;

namespace ImageStamp.Core.Validation;

/// <summary>
/// Comprobaciones aplicadas a cualquier fichero subido (imagen base o capa de imagen),
/// antes de que su contenido llegue al pipeline de composición.
/// </summary>
public static class UploadValidation
{
    public static string? ValidateUpload(IFormFile file, ImageProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);

        if (file.Length == 0)
        {
            return "The uploaded file '" + file.FileName + "' is empty.";
        }

        if (file.Length > options.MaxUploadBytes)
        {
            return "The uploaded file '" + file.FileName + "' is larger than the "
                + options.MaxUploadBytes + " byte limit.";
        }

        if (!options.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return "The content type '" + file.ContentType + "' is not accepted. Expected one of: "
                + string.Join(", ", options.AllowedContentTypes) + ".";
        }

        return null;
    }
}