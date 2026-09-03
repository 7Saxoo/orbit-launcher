using Orbit.Core.Data;
using Orbit.Core.Models;
using Orbit.Tests.TestSupport;
using Serilog.Core;

namespace Orbit.Tests;

public class SqliteAppRepositoryTests
{
    private static (SqliteAppRepository repo, SqliteConnectionFactory factory) NewRepo(TempWorkspace ws)
    {
        var factory = new SqliteConnectionFactory(ws.Paths);
        new DatabaseInitializer(factory, Logger.None).Initialize();
        return (new SqliteAppRepository(factory, Logger.None), factory);
    }

    private static AppEntry SampleEntry(string name = "Sample", string? path = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        ExecutablePath = path ?? @"C:\Program Files (x86)\Sample\sample.exe",
        Category = "Utilitaires",
        Kind = AppKind.Application,
        Description = "desc",
        Arguments = "--flag \"a b\"",
        DateAdded = DateTimeOffset.Now,
    };

    [Fact]
    public async Task Add_then_GetById_round_trips_all_fields()
    {
        using var ws = new TempWorkspace();
        var (repo, _) = NewRepo(ws);
        var entry = SampleEntry();

        await repo.AddAsync(entry);
        var loaded = await repo.GetByIdAsync(entry.Id);

        Assert.NotNull(loaded);
        Assert.Equal(entry.Name, loaded!.Name);
        Assert.Equal(entry.ExecutablePath, loaded.ExecutablePath);
        Assert.Equal(entry.Arguments, loaded.Arguments);
        Assert.Equal(entry.Category, loaded.Category);
        Assert.Equal(entry.Kind, loaded.Kind);
        Assert.Equal(entry.DateAdded.ToUnixTimeSeconds(), loaded.DateAdded.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Data_survives_a_fresh_connection_factory()
    {
        using var ws = new TempWorkspace();
        var entry = SampleEntry("Persisted");

        {
            var (repo, _) = NewRepo(ws);
            await repo.AddAsync(entry);
        }

        // Simulate an app restart: brand new factory + repo over the same file.
        var factory2 = new SqliteConnectionFactory(ws.Paths);
        var repo2 = new SqliteAppRepository(factory2, Logger.None);
        var all = await repo2.GetAllAsync();

        Assert.Single(all);
        Assert.Equal("Persisted", all[0].Name);
    }

    [Fact]
    public async Task ExistsByPath_matches_case_insensitively_and_after_normalisation()
    {
        using var ws = new TempWorkspace();
        var (repo, _) = NewRepo(ws);
        await repo.AddAsync(SampleEntry(path: @"C:\Games\Mon Jeu\Game.exe"));

        Assert.True(await repo.ExistsByPathAsync(@"c:\games\mon jeu\game.exe"));
        Assert.True(await repo.ExistsByPathAsync("  \"C:\\Games\\Mon Jeu\\Game.exe\"  "));
        Assert.False(await repo.ExistsByPathAsync(@"C:\Games\Autre\Game.exe"));
    }

    [Fact]
    public async Task Update_persists_changes()
    {
        using var ws = new TempWorkspace();
        var (repo, _) = NewRepo(ws);
        var entry = SampleEntry();
        await repo.AddAsync(entry);

        entry.Name = "Renamed";
        entry.IsFavorite = true;
        entry.Category = "Jeux";
        await repo.UpdateAsync(entry);

        var loaded = await repo.GetByIdAsync(entry.Id);
        Assert.Equal("Renamed", loaded!.Name);
        Assert.True(loaded.IsFavorite);
        Assert.Equal("Jeux", loaded.Category);
    }

    [Fact]
    public async Task RecordLaunch_increments_count_and_sets_timestamp()
    {
        using var ws = new TempWorkspace();
        var (repo, _) = NewRepo(ws);
        var entry = SampleEntry();
        await repo.AddAsync(entry);

        await repo.RecordLaunchAsync(entry.Id, DateTimeOffset.Now);
        await repo.RecordLaunchAsync(entry.Id, DateTimeOffset.Now);

        var loaded = await repo.GetByIdAsync(entry.Id);
        Assert.Equal(2, loaded!.LaunchCount);
        Assert.NotNull(loaded.LastLaunchedAt);
    }

    [Fact]
    public async Task Delete_and_DeleteAll_remove_entries()
    {
        using var ws = new TempWorkspace();
        var (repo, _) = NewRepo(ws);
        var a = SampleEntry("A", @"C:\a\a.exe");
        var b = SampleEntry("B", @"C:\b\b.exe");
        await repo.AddAsync(a);
        await repo.AddAsync(b);

        await repo.DeleteAsync(a.Id);
        Assert.Single(await repo.GetAllAsync());

        await repo.DeleteAllAsync();
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task Launch_option_columns_round_trip()
    {
        using var ws = new TempWorkspace();
        var (repo, _) = NewRepo(ws);
        var entry = SampleEntry("Java Game");
        entry.RunAsAdmin = true;
        entry.JavaMaxMemoryMb = 4096;
        await repo.AddAsync(entry);

        var loaded = await repo.GetByIdAsync(entry.Id);
        Assert.True(loaded!.RunAsAdmin);
        Assert.Equal(4096, loaded.JavaMaxMemoryMb);

        loaded.RunAsAdmin = false;
        loaded.JavaMaxMemoryMb = null;
        await repo.UpdateAsync(loaded);

        var again = await repo.GetByIdAsync(entry.Id);
        Assert.False(again!.RunAsAdmin);
        Assert.Null(again.JavaMaxMemoryMb);
    }

    [Fact]
    public async Task Accented_and_spaced_paths_round_trip_unchanged()
    {
        using var ws = new TempWorkspace();
        var (repo, _) = NewRepo(ws);
        const string weird = @"D:\Jeux\Café des Développeurs (2024)\Jeu Spécial.exe";
        await repo.AddAsync(SampleEntry("Accents", weird));

        var loaded = (await repo.GetAllAsync()).Single();
        Assert.Equal(weird, loaded.ExecutablePath);
    }
}
