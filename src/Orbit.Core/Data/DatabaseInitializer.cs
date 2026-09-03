using Microsoft.Data.Sqlite;
using Serilog;

namespace Orbit.Core.Data;

/// <summary>
/// Owns the database schema. Uses SQLite's <c>PRAGMA user_version</c> as a tiny
/// migration counter so future schema changes are a matter of adding another
/// <c>case</c> below.
/// </summary>
public sealed class DatabaseInitializer
{
    private const int TargetVersion = 2;

    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger _log;

    public DatabaseInitializer(SqliteConnectionFactory factory, ILogger log)
    {
        _factory = factory;
        _log = log.ForContext<DatabaseInitializer>();
    }

    /// <summary>Creates or upgrades the schema. Safe to call on every start-up.</summary>
    public void Initialize()
    {
        using var connection = _factory.Create();

        var version = GetUserVersion(connection);
        _log.Debug("Database at {Path} is schema version {Version}", _factory.DatabaseFilePath, version);

        while (version < TargetVersion)
        {
            var next = version + 1;
            using var tx = connection.BeginTransaction();
            ApplyMigration(connection, tx, next);
            SetUserVersion(connection, tx, next);
            tx.Commit();
            version = next;
            _log.Information("Applied database migration to version {Version}", version);
        }
    }

    private static void ApplyMigration(SqliteConnection connection, SqliteTransaction tx, int version)
    {
        switch (version)
        {
            case 1:
                Execute(connection, tx, """
                    CREATE TABLE app_entries (
                        id                TEXT    NOT NULL PRIMARY KEY,
                        name              TEXT    NOT NULL,
                        executable_path   TEXT    NOT NULL,
                        arguments         TEXT        NULL,
                        working_directory TEXT        NULL,
                        kind              INTEGER NOT NULL DEFAULT 0,
                        category          TEXT    NOT NULL DEFAULT '',
                        description       TEXT        NULL,
                        icon_cache_path   TEXT        NULL,
                        date_added        TEXT    NOT NULL,
                        launch_count      INTEGER NOT NULL DEFAULT 0,
                        last_launched_at  TEXT        NULL,
                        is_favorite       INTEGER NOT NULL DEFAULT 0,
                        genre             TEXT        NULL,
                        platform          TEXT        NULL,
                        publisher         TEXT        NULL,
                        cover_image_path  TEXT        NULL,
                        play_time_seconds INTEGER     NULL
                    );
                    """);
                Execute(connection, tx,
                    "CREATE INDEX ix_app_entries_executable_path ON app_entries (executable_path COLLATE NOCASE);");
                break;

            case 2:
                Execute(connection, tx,
                    "ALTER TABLE app_entries ADD COLUMN run_as_admin INTEGER NOT NULL DEFAULT 0;");
                Execute(connection, tx,
                    "ALTER TABLE app_entries ADD COLUMN java_max_memory_mb INTEGER NULL;");
                break;

            default:
                throw new InvalidOperationException($"No migration defined for schema version {version}.");
        }
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void SetUserVersion(SqliteConnection connection, SqliteTransaction tx, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        // PRAGMA does not accept parameters; version is a trusted int constant.
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction tx, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
