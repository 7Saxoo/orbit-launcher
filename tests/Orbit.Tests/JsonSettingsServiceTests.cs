using Orbit.Core.Services;
using Orbit.Tests.TestSupport;
using Serilog.Core;

namespace Orbit.Tests;

public class JsonSettingsServiceTests
{
    [Fact]
    public void Load_creates_a_default_file_on_first_run()
    {
        using var ws = new TempWorkspace();
        var service = new JsonSettingsService(ws.Paths, Logger.None);

        service.Load();

        Assert.True(File.Exists(ws.Paths.SettingsFile));
        Assert.Equal(ThemePreference.System, service.Current.Theme);
        Assert.True(service.Current.ConfirmBeforeRemove);
    }

    [Fact]
    public async Task Save_then_Load_round_trips_values()
    {
        using var ws = new TempWorkspace();
        var service = new JsonSettingsService(ws.Paths, Logger.None);
        service.Load();

        await service.SaveAsync(new AppSettings
        {
            Theme = ThemePreference.Dark,
            Sort = LibrarySort.MostLaunched,
            ConfirmBeforeRemove = false,
            LastSection = "Games"
        });

        var reloaded = new JsonSettingsService(ws.Paths, Logger.None);
        reloaded.Load();

        Assert.Equal(ThemePreference.Dark, reloaded.Current.Theme);
        Assert.Equal(LibrarySort.MostLaunched, reloaded.Current.Sort);
        Assert.False(reloaded.Current.ConfirmBeforeRemove);
        Assert.Equal("Games", reloaded.Current.LastSection);
    }

    [Fact]
    public void Load_recovers_from_a_corrupt_file()
    {
        using var ws = new TempWorkspace();
        File.WriteAllText(ws.Paths.SettingsFile, "{ this is not valid json ");

        var service = new JsonSettingsService(ws.Paths, Logger.None);
        service.Load();

        Assert.Equal(ThemePreference.System, service.Current.Theme);
        var quarantined = Directory.GetFiles(ws.Paths.BaseDirectory, "settings.json.corrupt-*");
        Assert.NotEmpty(quarantined);
    }

    [Fact]
    public async Task Save_raises_Changed()
    {
        using var ws = new TempWorkspace();
        var service = new JsonSettingsService(ws.Paths, Logger.None);
        service.Load();

        var raised = false;
        service.Changed += (_, _) => raised = true;
        await service.SaveAsync(new AppSettings { Theme = ThemePreference.Light });

        Assert.True(raised);
    }
}
