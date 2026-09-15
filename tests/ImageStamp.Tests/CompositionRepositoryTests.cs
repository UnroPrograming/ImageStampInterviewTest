using ImageStamp.Core.Models;
using ImageStamp.Infrastructure.Persistence;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace ImageStamp.Tests;

/// <summary>
/// Marks a test that needs a reachable PostgreSQL instance. When <c>IMAGESTAMP_TEST_POSTGRES</c> is
/// not set the test is reported as <em>skipped</em> rather than passing, so that a run without a
/// database cannot be mistaken for a run that verified persistence.
/// </summary>
public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public RequiresPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(PostgresTestDatabase.ConnectionString))
        {
            Skip = "Set IMAGESTAMP_TEST_POSTGRES to run the persistence tests.";
        }
    }
}

public static class PostgresTestDatabase
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("IMAGESTAMP_TEST_POSTGRES");
}

/// <summary>
/// Persistence tests. They only run when a PostgreSQL instance is reachable, which is signalled by
/// the <c>IMAGESTAMP_TEST_POSTGRES</c> environment variable, for example:
/// <code>
/// docker compose up -d postgres
/// set IMAGESTAMP_TEST_POSTGRES=Host=localhost;Port=15432;Database=imagestamp;Username=imagestamp;Password=imagestamp
/// dotnet test
/// </code>
/// </summary>
public sealed class CompositionRepositoryTests
{
    [RequiresPostgresFact]
    public async Task CreateAndRead_RoundTripsACompositionWithItsLayers()
    {
        string connectionString = PostgresTestDatabase.ConnectionString!;

        CompositionRepository repository = new CompositionRepository(
            connectionString,
            NullLogger<CompositionRepository>.Instance);

        Composition composition = new Composition();
        composition.Id = Guid.NewGuid();
        composition.CreatedAt = DateTimeOffset.UtcNow;
        composition.Status = CompositionStatus.Pending;
        composition.BaseImageFileName = "base.png";
        composition.LayerCount = 2;

        await repository.CreateCompositionAsync(composition, CancellationToken.None);

        CompositionLayer imageLayer = new CompositionLayer();
        imageLayer.Id = Guid.NewGuid();
        imageLayer.CompositionId = composition.Id;
        imageLayer.LayerType = LayerTypes.Image;
        imageLayer.X = 10;
        imageLayer.Y = 20;
        imageLayer.Opacity = 1f;
        imageLayer.ZIndex = 1;
        imageLayer.FileName = "logo.png";

        CompositionLayer solidLayer = new CompositionLayer();
        solidLayer.Id = Guid.NewGuid();
        solidLayer.CompositionId = composition.Id;
        solidLayer.LayerType = LayerTypes.Solid;
        solidLayer.X = 100;
        solidLayer.Y = 50;
        solidLayer.Opacity = 0.5f;
        solidLayer.ZIndex = 3;
        solidLayer.Width = 300;
        solidLayer.Height = 100;
        solidLayer.Color = "#FF0000";

        await repository.AddLayerAsync(imageLayer, CancellationToken.None);
        await repository.AddLayerAsync(solidLayer, CancellationToken.None);
        await repository.MarkCompletedAsync(composition.Id, 4096, 42, CancellationToken.None);

        Composition? stored = await repository.GetCompositionAsync(composition.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(CompositionStatus.Completed, stored!.Status);
        Assert.Equal(4096, stored.OutputSizeBytes);
        Assert.Equal(42, stored.ProcessingTimeMs);
        Assert.Equal("base.png", stored.BaseImageFileName);

        List<CompositionLayer> layers = await repository.GetLayersAsync(composition.Id, CancellationToken.None);

        Assert.Equal(2, layers.Count);
        Assert.Equal(LayerTypes.Image, layers[0].LayerType);
        Assert.Equal("logo.png", layers[0].FileName);
        Assert.Equal(LayerTypes.Solid, layers[1].LayerType);
        Assert.Equal("#FF0000", layers[1].Color);
        Assert.Equal(300, layers[1].Width);
    }
}
