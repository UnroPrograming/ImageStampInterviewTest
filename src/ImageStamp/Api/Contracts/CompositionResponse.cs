using ImageStamp.Core.Models;

namespace ImageStamp.Api.Contracts;

/// <summary>
/// Composition metadata as returned by the query endpoints.
/// </summary>
public sealed class CompositionResponse
{
    public CompositionResponse()
    {
        Status = CompositionStatus.Pending;
        Layers = new List<CompositionLayerResponse>();
    }

    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Status { get; set; }

    public string? BaseImageFileName { get; set; }

    public int LayerCount { get; set; }

    public long? OutputSizeBytes { get; set; }

    public int? ProcessingTimeMs { get; set; }

    public string? ErrorMessage { get; set; }

    public List<CompositionLayerResponse> Layers { get; set; }

    public static CompositionResponse From(Composition composition)
    {
        CompositionResponse response = new CompositionResponse();

        response.Id = composition.Id;
        response.CreatedAt = composition.CreatedAt;
        response.Status = composition.Status;
        response.BaseImageFileName = composition.BaseImageFileName;
        response.LayerCount = composition.LayerCount;
        response.OutputSizeBytes = composition.OutputSizeBytes;
        response.ProcessingTimeMs = composition.ProcessingTimeMs;
        response.ErrorMessage = composition.ErrorMessage;

        foreach (CompositionLayer layer in composition.Layers)
        {
            response.Layers.Add(CompositionLayerResponse.From(layer));
        }

        return response;
    }
}

public sealed class CompositionLayerResponse
{
    public Guid Id { get; set; }

    public string LayerType { get; set; } = LayerTypes.Image;

    public int X { get; set; }

    public int Y { get; set; }

    public float Opacity { get; set; }

    public int ZIndex { get; set; }

    public string? FileName { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? Color { get; set; }

    public static CompositionLayerResponse From(CompositionLayer layer)
    {
        CompositionLayerResponse response = new CompositionLayerResponse();

        response.Id = layer.Id;
        response.LayerType = layer.LayerType;
        response.X = layer.X;
        response.Y = layer.Y;
        response.Opacity = layer.Opacity;
        response.ZIndex = layer.ZIndex;
        response.FileName = layer.FileName;
        response.Width = layer.Width;
        response.Height = layer.Height;
        response.Color = layer.Color;

        return response;
    }
}
