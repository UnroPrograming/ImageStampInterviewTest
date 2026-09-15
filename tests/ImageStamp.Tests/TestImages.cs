using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageStamp.Tests;

/// <summary>
/// Small helpers so that the tests do not depend on binary fixtures on disk.
/// </summary>
internal static class TestImages
{
    /// <summary>
    /// Creates a PNG of the requested size, filled with a single colour.
    /// </summary>
    public static byte[] SolidPng(int width, int height, Rgba32 color)
    {
        using Image<Rgba32> image = new Image<Rgba32>(width, height, color);
        using MemoryStream buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return buffer.ToArray();
    }

    public static MemoryStream SolidPngStream(int width, int height, Rgba32 color)
    {
        return new MemoryStream(SolidPng(width, height, color));
    }

    /// <summary>
    /// Decodes a PNG so that individual pixels can be asserted.
    /// </summary>
    public static Image<Rgba32> Decode(byte[] png)
    {
        return Image.Load<Rgba32>(png);
    }

    public static readonly Rgba32 Red = new Rgba32(255, 0, 0, 255);

    public static readonly Rgba32 Blue = new Rgba32(0, 0, 255, 255);

    public static readonly Rgba32 Green = new Rgba32(0, 255, 0, 255);

    public static readonly Rgba32 Transparent = new Rgba32(0, 0, 0, 0);
}
