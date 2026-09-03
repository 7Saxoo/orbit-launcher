using System.Globalization;
using Microsoft.Data.Sqlite;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Serilog;

namespace Orbit.Core.Data;

/// <summary>SQLite-backed <see cref="IAppRepository"/>. All access goes through
/// parameterised commands; no user value is ever concatenated into SQL.</summary>
public sealed class SqliteAppRepository : IAppRepository
{
    private const string Columns =
        "id, name, executable_path, arguments, working_directory, kind, category, description, " +
        "icon_cache_path, date_added, launch_count, last_launched_at, is_favorite, " +
        "genre, platform, publisher, cover_image_path, play_time_seconds";

    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger _log;

    public SqliteAppRepository(SqliteConnectionFactory factory, ILogger log)
    {
        _factory = factory;
        _log = log.ForContext<SqliteAppRepository>();
    }

    public async Task<IReadOnlyList<AppEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM app_entries ORDER BY name COLLATE NOCASE;";

        var results = new List<AppEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(Map(reader));

        return results;
    }

    public async Task<AppEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM app_entries WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Map(reader) : null;
    }

    public async Task<bool> ExistsByPathAsync(string executablePath, CancellationToken ct = default)
    {
        var normalized = PathHelper.Normalize(executablePath);
        await using var connection = _factory.Create();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT EXISTS(SELECT 1 FROM app_entries WHERE executable_path = $p COLLATE NOCASE);";
        cmd.Parameters.AddWithValue("$p", normalized);

        var scalar = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture) == 1;
    }

    public async Task AddAsync(AppEntry entry, CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO app_entries ({Columns})
            VALUES ($id, $name, $path, $args, $wd, $kind, $category, $description,
                    $icon, $added, $count, $last, $fav,
                    $genre, $platform, $publisher, $cover, $playtime);
            """;
        Bind(cmd, entry);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        _log.Information("Inserted entry {Id} ({Name})", entry.Id, entry.Name);
    }

    public async Task UpdateAsync(AppEntry entry, CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE app_entries SET
                name = $name, executable_path = $path, arguments = $args,
                working_directory = $wd, kind = $kind, category = $category,
                description = $description, icon_cache_path = $icon,
                date_added = $added, launch_count = $count, last_launched_at = $last,
                is_favorite = $fav, genre = $genre, platform = $platform,
                publisher = $publisher, cover_image_path = $cover,
                play_time_seconds = $playtime
            WHERE id = $id;
            """;
        Bind(cmd, entry);
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (rows == 0)
            throw new InvalidOperationException($"No entry with id {entry.Id:D} to update.");
        _log.Information("Updated entry {Id} ({Name})", entry.Id, entry.Name);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM app_entries WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        _log.Information("Deleted entry {Id}", id);
    }

    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM app_entries;";
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        _log.Warning("Cleared all {Count} entries (reset)", rows);
    }

    public async Task RecordLaunchAsync(Guid id, DateTimeOffset launchedAt, CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE app_entries
            SET launch_count = launch_count + 1, last_launched_at = $last
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        cmd.Parameters.AddWithValue("$last", launchedAt.ToString("O", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void Bind(SqliteCommand cmd, AppEntry e)
    {
        cmd.Parameters.AddWithValue("$id", e.Id.ToString("D"));
        cmd.Parameters.AddWithValue("$name", e.Name);
        cmd.Parameters.AddWithValue("$path", PathHelper.Normalize(e.ExecutablePath));
        cmd.Parameters.AddWithValue("$args", (object?)e.Arguments ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$wd", (object?)e.WorkingDirectory ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$kind", (int)e.Kind);
        cmd.Parameters.AddWithValue("$category", e.Category ?? string.Empty);
        cmd.Parameters.AddWithValue("$description", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$icon", (object?)e.IconCachePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$added", e.DateAdded.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$count", e.LaunchCount);
        cmd.Parameters.AddWithValue("$last",
            e.LastLaunchedAt.HasValue
                ? e.LastLaunchedAt.Value.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value);
        cmd.Parameters.AddWithValue("$fav", e.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$genre", (object?)e.Genre ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$platform", (object?)e.Platform ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$publisher", (object?)e.Publisher ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cover", (object?)e.CoverImagePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$playtime", (object?)e.PlayTimeSeconds ?? DBNull.Value);
    }

    private static AppEntry Map(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(0)),
        Name = r.GetString(1),
        ExecutablePath = r.GetString(2),
        Arguments = r.IsDBNull(3) ? null : r.GetString(3),
        WorkingDirectory = r.IsDBNull(4) ? null : r.GetString(4),
        Kind = (AppKind)r.GetInt32(5),
        Category = r.GetString(6),
        Description = r.IsDBNull(7) ? null : r.GetString(7),
        IconCachePath = r.IsDBNull(8) ? null : r.GetString(8),
        DateAdded = ParseDate(r.GetString(9)) ?? DateTimeOffset.Now,
        LaunchCount = r.GetInt32(10),
        LastLaunchedAt = r.IsDBNull(11) ? null : ParseDate(r.GetString(11)),
        IsFavorite = r.GetInt32(12) != 0,
        Genre = r.IsDBNull(13) ? null : r.GetString(13),
        Platform = r.IsDBNull(14) ? null : r.GetString(14),
        Publisher = r.IsDBNull(15) ? null : r.GetString(15),
        CoverImagePath = r.IsDBNull(16) ? null : r.GetString(16),
        PlayTimeSeconds = r.IsDBNull(17) ? null : r.GetInt64(17)
    };

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
}
