using Microsoft.Data.Sqlite;
using Orbit.Core.Infrastructure;

namespace Orbit.Core.Data;

/// <summary>Creates open <see cref="SqliteConnection"/> instances pointing at the
/// launcher's database file. Centralised so the connection string (and its
/// pragmas) is defined in exactly one place.</summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(OrbitPaths paths)
        : this(paths.DatabaseFile)
    {
    }

    public SqliteConnectionFactory(string databaseFilePath)
    {
        DatabaseFilePath = databaseFilePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ToString();
    }

    public string DatabaseFilePath { get; }

    public SqliteConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        // WAL keeps reads and writes from blocking each other; NORMAL sync is a
        // safe durability/perf trade-off for a local single-user app.
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
