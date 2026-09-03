using Microsoft.Data.Sqlite;
using Orbit.Core.Data;
using Orbit.Tests.TestSupport;
using Serilog.Core;

namespace Orbit.Tests;

public class DatabaseTests
{
    [Fact]
    public void Initialize_creates_schema_and_sets_user_version()
    {
        using var ws = new TempWorkspace();
        var factory = new SqliteConnectionFactory(ws.Paths);

        new DatabaseInitializer(factory, Logger.None).Initialize();

        using var connection = factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        Assert.Equal(1, Convert.ToInt32(cmd.ExecuteScalar()));

        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='app_entries';";
        Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void Initialize_is_idempotent()
    {
        using var ws = new TempWorkspace();
        var factory = new SqliteConnectionFactory(ws.Paths);
        var initializer = new DatabaseInitializer(factory, Logger.None);

        initializer.Initialize();
        var ex = Record.Exception(() => initializer.Initialize());

        Assert.Null(ex);
    }
}
