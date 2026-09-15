namespace ImageStamp.Core.Models;

public static class CompositionStatus
{
    public const string Pending = "pending";

    public const string Completed = "completed";

    public const string Failed = "failed";
}

/// <summary>
/// Metadata stored for every composition request. The binaries themselves are not persisted.
/// </summary>
public sealed class Composition
{
    public Composition()
    {
        Status = CompositionStatus.Pending;
        Layers = new List<CompositionLayer>();
    }

    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Status { get; set; }

    public string? BaseImageFileName { get; set; }

    public int LayerCount { get; set; }

    public long? OutputSizeBytes { get; set; }

    public int? ProcessingTimeMs { get; set; }

    public string? ErrorMessage { get; set; }

    public List<CompositionLayer> Layers { get; set; }
}

/// <summary>
/// Metadata of a single layer of a stored composition.
/// </summary>
public sealed class CompositionLayer
{
    public Guid Id { get; set; }

    public Guid CompositionId { get; set; }

    public string LayerType { get; set; } = LayerTypes.Image;

    public int X { get; set; }

    public int Y { get; set; }

    public float Opacity { get; set; }

    public int ZIndex { get; set; }

    public string? FileName { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? Color { get; set; }
}
