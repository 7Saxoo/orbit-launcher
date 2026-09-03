using System.Diagnostics;
using Orbit.Core.Data;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Orbit.Tests.TestSupport;
using Serilog.Core;

namespace Orbit.Tests;

/// <summary>
/// End-to-end exercise of the real Core stack: real SQLite repository, real
/// executable inspector, real icon extraction. Only the process launcher is a
/// fake, so no external programs are actually started.
/// </summary>
public class LibraryIntegrationTests
{
    private static string ThisProcessExe => Process.GetCurrentProcess().MainModule!.FileName;

    private static (LibraryService service, FakeProcessLauncher launcher) BuildRealStack(TempWorkspace ws)
    {
        var factory = new SqliteConnectionFactory(ws.Paths);
        new DatabaseInitializer(factory, Logger.None).Initialize();

        var repo = new SqliteAppRepository(factory, Logger.None);
        var inspector = new ExecutableInspector();
        var icons = new IconService(ws.Paths, Logger.None);
        var launcher = new FakeProcessLauncher();

        return (new LibraryService(repo, inspector, icons, launcher, ws.Paths, Logger.None), launcher);
    }

    [Fact]
    public async Task Full_lifecycle_add_reload_detect_missing_fix_and_launch()
    {
        using var ws = new TempWorkspace();
        var (service, launcher) = BuildRealStack(ws);

        // A real copy of an executable we can delete later.
        var exeCopy = Path.Combine(ws.Root, "Mon Jeu", "Game.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exeCopy)!);
        File.Copy(ThisProcessExe, exeCopy);

        // 1. Add
        var entry = await service.AddAsync(new NewAppRequest
        {
            ExecutablePath = exeCopy,
            Kind = AppKind.Game,
            Category = "Test"
        });
        Assert.False(string.IsNullOrWhiteSpace(entry.Name));
        Assert.NotNull(entry.IconCachePath);
        Assert.True(File.Exists(entry.IconCachePath!));

        // 2. Reload from a brand new stack (simulated restart) -> Available
        var (service2, launcher2) = BuildRealStack(ws);
        var afterRestart = await service2.LoadAsync();
        Assert.Single(afterRestart);
        Assert.Equal(AppAvailability.Available, afterRestart[0].Availability);

        // 3. The file disappears -> Missing, but the entry stays
        File.Delete(exeCopy);
        var afterDelete = await service2.LoadAsync();
        Assert.Single(afterDelete);
        Assert.Equal(AppAvailability.Missing, afterDelete[0].Availability);

        // 4. Launching a missing file fails and records nothing
        var badLaunch = await service2.LaunchAsync(entry.Id);
        Assert.False(badLaunch.Succeeded);
        Assert.Equal(LaunchStatus.FileNotFound, badLaunch.Status);

        // 5. Fix the path to a valid exe, then launch succeeds and stats update
        var fixup = afterDelete[0].Entry.Clone();
        fixup.ExecutablePath = ThisProcessExe;
        await service2.UpdateAsync(fixup);

        var goodLaunch = await service2.LaunchAsync(entry.Id);
        Assert.True(goodLaunch.Succeeded);
        Assert.Single(launcher2.Launched);

        var reloaded = (await service2.LoadAsync())[0].Entry;
        Assert.Equal(1, reloaded.LaunchCount);
        Assert.NotNull(reloaded.LastLaunchedAt);
    }

    [Fact]
    public async Task Reset_clears_entries_and_icon_cache_but_not_source_files()
    {
        using var ws = new TempWorkspace();
        var (service, _) = BuildRealStack(ws);

        var exeCopy = Path.Combine(ws.Root, "keep.exe");
        File.Copy(ThisProcessExe, exeCopy);
        await service.AddAsync(new NewAppRequest { ExecutablePath = exeCopy });

        Assert.NotEmpty(Directory.GetFiles(ws.Paths.IconCacheDirectory));

        await service.ResetAsync();

        Assert.Empty(await service.LoadAsync());
        Assert.Empty(Directory.GetFiles(ws.Paths.IconCacheDirectory));
        Assert.True(File.Exists(exeCopy)); // source executable untouched
    }
}
