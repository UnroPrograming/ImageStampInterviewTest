using System.Text.Json;

using ImageStamp.Api.Contracts;
using ImageStamp.Core.Imaging;
using ImageStamp.Core.Models;
using ImageStamp.Core.Options;
using ImageStamp.Core.Validation;
using ImageStamp.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using SixLabors.ImageSharp;

namespace ImageStamp.Api.Controllers;

[ApiController]
[Route("api/compositions")]
public sealed class CompositionsController(
    ImageCompositionService compositionService,
    CompositionRepository repository,
    IOptions<ImageProcessingOptions> options,
    ILogger<CompositionsController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions LayerJsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ImageProcessingOptions _options = options.Value;

    /// <summary>
    /// Composes a base PNG with the submitted layers and returns the resulting PNG.
    /// </summary>
    /// <remarks>
    /// Expects a <c>multipart/form-data</c> body with:
    /// <list type="bullet">
    /// <item><description><c>baseImage</c>: the background PNG file.</description></item>
    /// <item><description><c>layers</c>: a JSON array describing the layers.</description></item>
    /// <item><description>one additional file per image layer, named after its <c>imageKey</c>.</description></item>
    /// </list>
    /// </remarks>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Produces("image/png", "application/problem+json")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return Problem("The request must be sent as multipart/form-data.", statusCode: StatusCodes.Status400BadRequest);
        }

        IFormCollection form = await Request.ReadFormAsync(cancellationToken);

        IFormFile? baseImageFile = form.Files["baseImage"];

        if (baseImageFile is null)
        {
            return Problem("A 'baseImage' file is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string? uploadError = ValidateUpload(baseImageFile);

        if (uploadError is not null)
        {
            return Problem(uploadError, statusCode: StatusCodes.Status400BadRequest);
        }

        LayerRequest[] layerRequests;

        try
        {
            string json = form["layers"].ToString();
            layerRequests = string.IsNullOrWhiteSpace(json)
                ? Array.Empty<LayerRequest>()
                : JsonSerializer.Deserialize<LayerRequest[]>(json, LayerJsonOptions) ?? Array.Empty<LayerRequest>();
        }
        catch (JsonException exception)
        {
            return Problem("The 'layers' field is not valid JSON: " + exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        CompositionRequest compositionRequest = new CompositionRequest();
        compositionRequest.BaseImageFileName = baseImageFile.FileName;

        // The base image is the payload we inspect the most, so it is materialised once up front.
        byte[] baseImageContent = await StreamHelpers.ReadFullyAsync(baseImageFile.OpenReadStream(), cancellationToken);
        compositionRequest.BaseImage = new MemoryStream(baseImageContent, writable: false);

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
                    return Problem(
                        "Layer " + i + ": no uploaded file matches the image key '" + layerRequest.ImageKey + "'.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                uploadError = ValidateUpload(layerFile);

                if (uploadError is not null)
                {
                    return Problem("Layer " + i + ": " + uploadError, statusCode: StatusCodes.Status400BadRequest);
                }

                layer.FileName = layerFile.FileName;
                layer.Image = layerFile.OpenReadStream();
            }
            else if (layer.Type == LayerTypes.Solid)
            {
                layer.Width = layerRequest.Width;
                layer.Height = layerRequest.Height;
                layer.Color = layerRequest.Color;
            }
            else
            {
                return Problem(
                    "Layer " + i + ": unsupported layer type '" + layerRequest.Type + "'.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            compositionRequest.Layers.Add(layer);
        }

        if (!CompositionValidation.TryValidate(compositionRequest, _options, out string validationError))
        {
            return Problem(validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        Composition composition = new Composition();
        composition.Id = Guid.NewGuid();
        composition.CreatedAt = DateTimeOffset.UtcNow;
        composition.Status = CompositionStatus.Pending;
        composition.BaseImageFileName = compositionRequest.BaseImageFileName;
        composition.LayerCount = compositionRequest.Layers.Count;

        await repository.CreateCompositionAsync(composition, cancellationToken);

        CompositionResult result;

        try
        {
            result = await compositionService.ComposeAsync(compositionRequest, cancellationToken);
        }
        catch (UnknownImageFormatException exception)
        {
            await repository.MarkFailedAsync(composition.Id, exception.Message, cancellationToken);
            return Problem("One of the uploaded files is not a supported image.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ImageFormatException exception)
        {
            await repository.MarkFailedAsync(composition.Id, exception.Message, cancellationToken);
            return Problem("One of the uploaded images could not be decoded.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Composition {CompositionId} failed.", composition.Id);
            await repository.MarkFailedAsync(composition.Id, exception.Message, cancellationToken);
            throw;
        }

        foreach (Layer layer in compositionRequest.Layers)
        {
            CompositionLayer stored = new CompositionLayer();
            stored.Id = Guid.NewGuid();
            stored.CompositionId = composition.Id;
            stored.LayerType = layer.Type;
            stored.X = layer.X;
            stored.Y = layer.Y;
            stored.Opacity = layer.Opacity;
            stored.ZIndex = layer.ZIndex;
            stored.FileName = layer.FileName;
            stored.Width = layer.Width;
            stored.Height = layer.Height;
            stored.Color = layer.Color;

            await repository.AddLayerAsync(stored, cancellationToken);
        }

        await repository.MarkCompletedAsync(
            composition.Id,
            result.OutputSizeBytes,
            (int)result.ProcessingTimeMs,
            cancellationToken);

        Response.Headers["X-Composition-Id"] = composition.Id.ToString();

        return File(result.Png, "image/png");
    }

    /// <summary>
    /// Lists the stored compositions, most recent first.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompositionResponse>>> GetAll(CancellationToken cancellationToken)
    {
        List<Composition> compositions = await repository.GetCompositionsAsync(cancellationToken);

        foreach (Composition composition in compositions)
        {
            composition.Layers = await repository.GetLayersAsync(composition.Id, cancellationToken);
        }

        List<CompositionResponse> response = new List<CompositionResponse>(compositions.Count);

        foreach (Composition composition in compositions)
        {
            response.Add(CompositionResponse.From(composition));
        }

        return Ok(response);
    }

    /// <summary>
    /// Returns a single composition together with its layers.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompositionResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        Composition? composition = await repository.GetCompositionAsync(id, cancellationToken);

        if (composition is null)
        {
            return NotFound();
        }

        composition.Layers = await repository.GetLayersAsync(composition.Id, cancellationToken);

        return Ok(CompositionResponse.From(composition));
    }

    private string? ValidateUpload(IFormFile file)
    {
        if (file.Length == 0)
        {
            return "The uploaded file '" + file.FileName + "' is empty.";
        }

        if (file.Length > _options.MaxUploadBytes)
        {
            return "The uploaded file '" + file.FileName + "' is larger than the "
                + _options.MaxUploadBytes + " byte limit.";
        }

        if (!_options.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return "The content type '" + file.ContentType + "' is not accepted. Expected one of: "
                + string.Join(", ", _options.AllowedContentTypes) + ".";
        }

        return null;
    }
}
