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
        Assert.Equal(ThemePreference.Dark, service.Current.Theme);
        Assert.Equal(AccentTemperature.Cool, service.Current.Temperature);
        Assert.True(service.Current.ConfirmBeforeRemove);
        Assert.True(service.Current.MinimizeToTrayOnClose);
        Assert.Equal(0.85, service.Current.UiScale);
        // 0 = "fit to screen on first launch"
        Assert.Equal(0, service.Current.WindowWidth);
        Assert.Equal(0, service.Current.WindowHeight);
    }

    [Fact]
    public async Task Save_then_Load_round_trips_values()
    {
        using var ws = new TempWorkspace();
        var service = new JsonSettingsService(ws.Paths, Logger.None);
        service.Load();

        await service.SaveAsync(new AppSettings
        {
            Theme = ThemePreference.Light,
            Temperature = AccentTemperature.Warm,
            Sort = LibrarySort.MostLaunched,
            ConfirmBeforeRemove = false,
            LastSection = "Games",
            WindowWidth = 1920,
            WindowHeight = 1080,
            WindowMaximized = true,
            MinimizeToTrayOnClose = false,
            UiScale = 1.15
        });

        var reloaded = new JsonSettingsService(ws.Paths, Logger.None);
        reloaded.Load();

        Assert.Equal(ThemePreference.Light, reloaded.Current.Theme);
        Assert.Equal(AccentTemperature.Warm, reloaded.Current.Temperature);
        Assert.Equal(LibrarySort.MostLaunched, reloaded.Current.Sort);
        Assert.False(reloaded.Current.ConfirmBeforeRemove);
        Assert.Equal("Games", reloaded.Current.LastSection);
        Assert.Equal(1920, reloaded.Current.WindowWidth);
        Assert.Equal(1080, reloaded.Current.WindowHeight);
        Assert.True(reloaded.Current.WindowMaximized);
        Assert.False(reloaded.Current.MinimizeToTrayOnClose);
        Assert.Equal(1.15, reloaded.Current.UiScale);
    }

    [Fact]
    public void Load_recovers_from_a_corrupt_file()
    {
        using var ws = new TempWorkspace();
        File.WriteAllText(ws.Paths.SettingsFile, "{ this is not valid json ");

        var service = new JsonSettingsService(ws.Paths, Logger.None);
        service.Load();

        Assert.Equal(ThemePreference.Dark, service.Current.Theme);
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
