namespace ImageStamp.Core.Models;

/// <summary>
/// Request HTTP para composición por lotes: aplica las mismas capas a múltiples imágenes base
/// en una única llamada, devolviendo un ZIP con todos los resultados.
/// </summary>
/// <remarks>
/// Caso de uso: stamping de logo + frame sobre 20-40 fotos de un vehículo desde cámara de concesionario.
/// Más eficiente que llamar N veces al endpoint single.
/// </remarks>
public sealed class BatchCompositionRequest
{
    public BatchCompositionRequest()
    {
        BaseImages = new List<BaseImageInput>();
        Layers = new List<Layer>();
    }

    /// <summary>
    /// Múltiples imágenes base PNG (2 o más). Cada una se procesará independientemente
    /// con las mismas capas y producirá una composición separada en la BD.
    /// </summary>
    public List<BaseImageInput> BaseImages { get; set; }

    /// <summary>
    /// Capas que se aplican a TODAS las imágenes base (compartidas).
    /// </summary>
    public List<Layer> Layers { get; set; }
}

/// <summary>
/// Una imagen base individual: su contenido y el nombre de archivo original.
/// Usado tanto por <see cref="CompositionRequest"/> (una sola) como por
/// <see cref="BatchCompositionRequest"/> (varias).
/// </summary>
public sealed class BaseImageInput
{
    public required Stream Content { get; set; }

    public string? FileName { get; set; }
}