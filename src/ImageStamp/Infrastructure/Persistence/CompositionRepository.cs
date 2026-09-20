using System.Data.Common;

using ImageStamp.Core.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace ImageStamp.Infrastructure.Persistence;

/// <summary>
/// Reads and writes composition metadata in PostgreSQL.
/// </summary>
public sealed class CompositionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CompositionRepository> _logger;

    public CompositionRepository(IConfiguration configuration, ILogger<CompositionRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        string? connectionString = configuration.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The 'Postgres' connection string is not configured.");
        }

        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Creates a repository bound to an explicit connection string. Used by the tests.
    /// </summary>
    public CompositionRepository(string connectionString, ILogger<CompositionRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task CreateCompositionAsync(Composition composition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(composition);

        const string sql = @"
INSERT INTO compositions (id, created_at, status, base_image_file_name, layer_count)
VALUES (@id, @created_at, @status, @base_image_file_name, @layer_count);";

        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", composition.Id);
        command.Parameters.AddWithValue("created_at", composition.CreatedAt);
        command.Parameters.AddWithValue("status", composition.Status);
        command.Parameters.AddWithValue("base_image_file_name", (object?)composition.BaseImageFileName ?? DBNull.Value);
        command.Parameters.AddWithValue("layer_count", composition.LayerCount);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddLayerAsync(CompositionLayer layer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layer);

        const string sql = @"
INSERT INTO composition_layers (id, composition_id, layer_type, x, y, opacity, z_index, file_name, width, height, color, sigma)
VALUES (@id, @composition_id, @layer_type, @x, @y, @opacity, @z_index, @file_name, @width, @height, @color, @sigma);";

        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", layer.Id);
        command.Parameters.AddWithValue("composition_id", layer.CompositionId);
        command.Parameters.AddWithValue("layer_type", layer.LayerType);
        command.Parameters.AddWithValue("x", layer.X);
        command.Parameters.AddWithValue("y", layer.Y);
        command.Parameters.AddWithValue("opacity", layer.Opacity);
        command.Parameters.AddWithValue("z_index", layer.ZIndex);
        command.Parameters.AddWithValue("file_name", (object?)layer.FileName ?? DBNull.Value);
        command.Parameters.AddWithValue("width", (object?)layer.Width ?? DBNull.Value);
        command.Parameters.AddWithValue("height", (object?)layer.Height ?? DBNull.Value);
        command.Parameters.AddWithValue("color", (object?)layer.Color ?? DBNull.Value);
        command.Parameters.AddWithValue("sigma", (object?)layer.Sigma ?? DBNull.Value);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task MarkCompletedAsync(Guid compositionId, long outputSizeBytes, int processingTimeMs, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE compositions
SET status = @status,
    output_size_bytes = @output_size_bytes,
    processing_time_ms = @processing_time_ms
WHERE id = @id;";

        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("status", CompositionStatus.Completed);
        command.Parameters.AddWithValue("output_size_bytes", outputSizeBytes);
        command.Parameters.AddWithValue("processing_time_ms", processingTimeMs);
        command.Parameters.AddWithValue("id", compositionId);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(Guid compositionId, string errorMessage, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE compositions
SET status = @status,
    error_message = @error_message
WHERE id = @id;";

        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("status", CompositionStatus.Failed);
        command.Parameters.AddWithValue("error_message", Truncate(errorMessage, 500));
        command.Parameters.AddWithValue("id", compositionId);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<List<Composition>> GetCompositionsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM compositions ORDER BY created_at DESC;";

        List<Composition> compositions = new List<Composition>();

        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            compositions.Add(ReadComposition(reader));
        }

        return compositions;
    }

    public async Task<Composition?> GetCompositionAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT id, created_at, status, base_image_file_name, layer_count, output_size_bytes, processing_time_ms, error_message
FROM compositions
WHERE id = @id;";

        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadComposition(reader);
    }

    public async Task<List<CompositionLayer>> GetLayersAsync(Guid compositionId, CancellationToken cancellationToken)
    {
        const string sql = @"
        SELECT id, composition_id, layer_type, x, y, opacity, z_index, file_name, width, height, color, sigma
        FROM composition_layers
        WHERE composition_id = @composition_id
        ORDER BY z_index;";

        List<CompositionLayer> layers = new List<CompositionLayer>();

        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("composition_id", compositionId);

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            layers.Add(ReadLayer(reader));
        }

        return layers;
    }

    /// <summary>
    /// Cheap connectivity probe used by the health endpoint.
    /// </summary>
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using NpgsqlCommand command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (NpgsqlException exception)
        {
            _logger.LogWarning(exception, "PostgreSQL is not reachable.");
            return false;
        }
    }

    private static Composition ReadComposition(DbDataReader reader)
    {
        Composition composition = new Composition();

        composition.Id = reader.GetGuid(reader.GetOrdinal("id"));
        composition.CreatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
        composition.Status = reader.GetString(reader.GetOrdinal("status"));

        int baseImageFileName = reader.GetOrdinal("base_image_file_name");
        composition.BaseImageFileName = reader.IsDBNull(baseImageFileName) ? null : reader.GetString(baseImageFileName);

        composition.LayerCount = reader.GetInt32(reader.GetOrdinal("layer_count"));

        int outputSizeBytes = reader.GetOrdinal("output_size_bytes");
        composition.OutputSizeBytes = reader.IsDBNull(outputSizeBytes) ? null : reader.GetInt64(outputSizeBytes);

        int processingTimeMs = reader.GetOrdinal("processing_time_ms");
        composition.ProcessingTimeMs = reader.IsDBNull(processingTimeMs) ? null : reader.GetInt32(processingTimeMs);

        int errorMessage = reader.GetOrdinal("error_message");
        composition.ErrorMessage = reader.IsDBNull(errorMessage) ? null : reader.GetString(errorMessage);

        return composition;
    }

    private static CompositionLayer ReadLayer(DbDataReader reader)
    {
        CompositionLayer layer = new CompositionLayer();

        layer.Id = reader.GetGuid(reader.GetOrdinal("id"));
        layer.CompositionId = reader.GetGuid(reader.GetOrdinal("composition_id"));
        layer.LayerType = reader.GetString(reader.GetOrdinal("layer_type"));
        layer.X = reader.GetInt32(reader.GetOrdinal("x"));
        layer.Y = reader.GetInt32(reader.GetOrdinal("y"));
        layer.Opacity = reader.GetFloat(reader.GetOrdinal("opacity"));
        layer.ZIndex = reader.GetInt32(reader.GetOrdinal("z_index"));

        int fileName = reader.GetOrdinal("file_name");
        layer.FileName = reader.IsDBNull(fileName) ? null : reader.GetString(fileName);

        int width = reader.GetOrdinal("width");
        layer.Width = reader.IsDBNull(width) ? null : reader.GetInt32(width);

        int height = reader.GetOrdinal("height");
        layer.Height = reader.IsDBNull(height) ? null : reader.GetInt32(height);

        int color = reader.GetOrdinal("color");
        layer.Color = reader.IsDBNull(color) ? null : reader.GetString(color);

        int sigma = reader.GetOrdinal("sigma");
        layer.Sigma = reader.IsDBNull(sigma) ? null : (float?)reader.GetFloat(sigma);

        return layer;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength);
    }
}
