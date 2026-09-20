using ImageStamp.Api.Contracts;
using ImageStamp.Core.Imaging;
using ImageStamp.Core.Models;
using ImageStamp.Core.Options;
using ImageStamp.Core.Validation;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace ImageStamp.Core.Mapping;

/// <summary>
/// Convierte el JSON del campo "layers" y los ficheros del form en <see cref="Layer"/> del
/// dominio. Usado tanto por el endpoint de composición simple como por el de batch, que
/// comparten exactamente el mismo formato de entrada.
/// </summary>
public static class LayerMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parsea el JSON de "layers" y construye la lista de <see cref="Layer"/> del dominio,
    /// resolviendo el fichero de cada capa de imagen contra el form.
    /// </summary>
    /// <returns>
    /// La lista de capas si todo es válido; en caso contrario, <c>layers</c> vacío y
    /// <c>error</c> con el primer problema encontrado (JSON inválido, fichero de capa
    /// ausente, o subida que no pasa <see cref="UploadValidation"/>).
    /// </returns>
    public static async Task<(List<Layer> layers, string? error)> ParseLayersAsync(
        string layersJson,
        IFormCollection form,
        ImageProcessingOptions options,
        CancellationToken cancellationToken)
    {
        LayerRequest[] layerRequests;

        try
        {
            layerRequests = string.IsNullOrWhiteSpace(layersJson)
                ? Array.Empty<LayerRequest>()
                : JsonSerializer.Deserialize<LayerRequest[]>(layersJson, JsonOptions) ?? Array.Empty<LayerRequest>();
        }
        catch (JsonException exception)
        {
            return (new List<Layer>(), "The 'layers' field is not valid JSON: " + exception.Message);
        }

        List<Layer> layers = new List<Layer>(layerRequests.Length);

        for (int i = 0; i < layerRequests.Length; i++)
        {
            LayerRequest layerRequest = layerRequests[i];

            Layer layer = new Layer();
            layer.Type = (layerRequest.Type ?? string.Empty).Trim().ToLowerInvariant();
            layer.X = layerRequest.X;
            layer.Y = layerRequest.Y;
            layer.ZIndex = layerRequest.ZIndex;
            layer.Opacity = layerRequest.Opacity;

            if (layer.Type == LayerTypes.Image)
            {
                IFormFile? layerFile = string.IsNullOrWhiteSpace(layerRequest.ImageKey)
                    ? null
                    : form.Files[layerRequest.ImageKey];

                if (layerFile is null)
                {
                    return (new List<Layer>(),
                        "Layer " + i + ": no uploaded file matches the image key '" + layerRequest.ImageKey + "'.");
                }

                string? uploadError = UploadValidation.ValidateUpload(layerFile, options);
                if (uploadError is not null)
                {
                    return (new List<Layer>(), "Layer " + i + ": " + uploadError);
                }

                layer.FileName = layerFile.FileName;

                // Se bufferiza una sola vez aquí, para que el llamador (Create o Batch) pueda
                // crear tantos MemoryStream frescos como necesite a partir de estos bytes; un
                // Stream leído desde IFormFile es de un solo uso. SetImageContent guarda los
                // bytes en la capa (no solo el stream), que es lo que permite a Layer.Clone()
                // generar un stream fresco por cada composición del batch.
                byte[] layerImageBytes = await StreamHelpers.ReadFullyAsync(layerFile.OpenReadStream(), cancellationToken);
                layer.SetImageContent(layerImageBytes);
            }
            else if (layer.Type == LayerTypes.Solid)
            {
                layer.Width = layerRequest.Width;
                layer.Height = layerRequest.Height;
                layer.Color = layerRequest.Color;
            }
            else if (layer.Type == LayerTypes.Blur)
            {
                layer.Width = layerRequest.Width;
                layer.Height = layerRequest.Height;
                layer.Sigma = layerRequest.Sigma;
            }
            else
            {
                return (new List<Layer>(), "Layer " + i + ": unsupported layer type '" + layerRequest.Type + "'.");
            }

            layers.Add(layer);
        }

        return (layers, null);
    }
}