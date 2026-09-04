using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.App.Infrastructure;
using Orbit.App.Services;
using Orbit.Core.Infrastructure;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>A theme choice plus the colours used to preview it in the dropdown.</summary>
public sealed record ThemeOption(ThemePreference Value, string Label, string Bg, string Fg, string Accent);

/// <summary>An accent-temperature choice plus its preview colours.</summary>
public sealed record TemperatureOption(AccentTemperature Value, string Label, string Bg, string Fg, string Accent);
public sealed record WindowSizeOption(int Width, int Height, bool Maximized, string Label);
public sealed record UiScaleOption(double Scale, string Label);

/// <summary>Backs the "Paramètres" page.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ThemeManager _themeManager;
    private readonly IDialogService _dialogs;
    private readonly ILibraryService _library;
    private readonly DetectionFlow _detectionFlow;
    private readonly IWindowService _windowService;
    private readonly OrbitPaths _paths;
    private readonly ILogger _log;

    private bool _suppressPersist;

    public SettingsViewModel(
        ISettingsService settings,
        ThemeManager themeManager,
        IDialogService dialogs,
        ILibraryService library,
        DetectionFlow detectionFlow,
        IWindowService windowService,
        OrbitPaths paths,
        ILogger log)
    {
        _settings = settings;
        _themeManager = themeManager;
        _dialogs = dialogs;
        _library = library;
        _detectionFlow = detectionFlow;
        _windowService = windowService;
        _paths = paths;
        _log = log.ForContext<SettingsViewModel>();

        _selectedTheme = ThemeOptions[0];
        _selectedTemperature = TemperatureOptions[0];
        _selectedSort = SortOptions[0];
        _selectedWindowSize = WindowSizeOptions[0];
        _selectedUiScale = UiScaleOptions[1];
        LoadFromSettings();
    }

    /// <summary>Set by <see cref="MainViewModel"/> so a data reset can refresh the shell.</summary>
    public ITileHost? Host { get; set; }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption(ThemePreference.System, "Système", "#26304A", "#E9EFFA", "#7B8CB0"),
        new ThemeOption(ThemePreference.Light,  "Clair",   "#F4F7FC", "#1A2233", "#2F6FED"),
        new ThemeOption(ThemePreference.Dark,   "Sombre",  "#0F1524", "#E9EFFA", "#4C7DF0"),
    };

    public IReadOnlyList<TemperatureOption> TemperatureOptions { get; } = new[]
    {
        new TemperatureOption(AccentTemperature.Cool, "Froide (bleu)",  "#1B2A4A", "#DCE6FA", "#4C7DF0"),
        new TemperatureOption(AccentTemperature.Warm, "Chaude (ambre)", "#3A2A1C", "#F5E7D6", "#F0975A"),
    };

    public IReadOnlyList<SortOption> SortOptions { get; } = new[]
    {
        new SortOption(LibrarySort.Name, "Nom (A → Z)"),
        new SortOption(LibrarySort.RecentlyAdded, "Ajout récent"),
        new SortOption(LibrarySort.MostLaunched, "Plus lancés"),
        new SortOption(LibrarySort.LastLaunched, "Dernier lancement"),
    };

    public IReadOnlyList<WindowSizeOption> WindowSizeOptions { get; } = new[]
    {
        new WindowSizeOption(0, 0, false, "Adaptée à l'écran"),
        new WindowSizeOption(1280, 720, false, "1280 × 720"),
        new WindowSizeOption(1600, 900, false, "1600 × 900"),
        new WindowSizeOption(1920, 1080, false, "1920 × 1080"),
        new WindowSizeOption(0, 0, true, "Maximisée"),
    };

    public IReadOnlyList<UiScaleOption> UiScaleOptions { get; } = new[]
    {
        new UiScaleOption(0.75, "Très compacte"),
        new UiScaleOption(0.85, "Compacte"),
        new UiScaleOption(1.00, "Normale"),
        new UiScaleOption(1.15, "Grande"),
    };

    [ObservableProperty] private ThemeOption _selectedTheme;
    [ObservableProperty] private TemperatureOption _selectedTemperature;
    [ObservableProperty] private SortOption _selectedSort;
    [ObservableProperty] private WindowSizeOption _selectedWindowSize;
    [ObservableProperty] private UiScaleOption _selectedUiScale;
    [ObservableProperty] private bool _confirmBeforeRemove = true;
    [ObservableProperty] private bool _minimizeToTrayOnClose = true;
    [ObservableProperty] private int _entryCount;

    [ObservableProperty] private string _igdbClientId = string.Empty;
    [ObservableProperty] private string _igdbClientSecret = string.Empty;
    [ObservableProperty] private string _steamGridDbApiKey = string.Empty;

    public string VersionText
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "Orbit" : $"Orbit v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public string CreditText => "developed by Saxo";

    public string DataFolderPath => _paths.BaseDirectory;

    public async Task RefreshAsync()
    {
        try
        {
            var items = await _library.LoadAsync().ConfigureAwait(true);
            EntryCount = items.Count;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not read entry count for settings page");
        }
    }

    [RelayCommand]
    private async Task ScanForAppsAsync()
    {
        var imported = _detectionFlow.Run();
        if (imported <= 0)
            return;

        await RefreshAsync().ConfigureAwait(true);
        if (Host is not null)
            await Host.RefreshAllAsync().ConfigureAwait(true);
        Host?.SetStatus($"{imported} application(s) importée(s) depuis la détection automatique.",
            StatusSeverity.Success);
    }

    [RelayCommand]
    private async Task RefreshIconsAsync()
    {
        try
        {
            var changed = await _library.RefreshIconsAsync().ConfigureAwait(true);
            if (Host is not null)
                await Host.RefreshAllAsync().ConfigureAwait(true);
            Host?.SetStatus(
                changed > 0 ? $"{changed} icône(s) mise(s) à jour." : "Icônes déjà à jour.",
                StatusSeverity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Icon refresh failed");
            _dialogs.ShowError("Rafraîchissement impossible", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        _paths.EnsureDirectories();
        _dialogs.RevealInExplorer(_paths.BaseDirectory);
    }

    [RelayCommand]
    private async Task ResetDataAsync()
    {
        if (!_dialogs.Confirm("Réinitialiser les données",
                "Toutes les applications enregistrées et le cache d'icônes seront supprimés.\n\n" +
                "Les fichiers .exe d'origine ne sont pas touchés. Continuer ?"))
        {
            return;
        }

        if (!_dialogs.Confirm("Confirmation finale",
                "Cette action est irréversible. Réinitialiser définitivement la bibliothèque ?"))
        {
            return;
        }

        try
        {
            await _library.ResetAsync().ConfigureAwait(true);
            _log.Warning("User reset all library data");
            await RefreshAsync().ConfigureAwait(true);
            if (Host is not null)
                await Host.RefreshAllAsync().ConfigureAwait(true);
            Host?.SetStatus("La bibliothèque a été réinitialisée.", StatusSeverity.Warning);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Data reset failed");
            _dialogs.ShowError("Réinitialisation impossible", ex.Message);
        }
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        _themeManager.Apply(value.Value, SelectedTemperature.Value);
        Persist();
    }

    partial void OnSelectedTemperatureChanged(TemperatureOption value)
    {
        _themeManager.Apply(SelectedTheme.Value, value.Value);
        Persist();
    }

    partial void OnSelectedSortChanged(SortOption value) => Persist();

    partial void OnSelectedUiScaleChanged(UiScaleOption value)
    {
        if (_suppressPersist)
            return;

        UiScaleManager.Set(value.Scale);
        Persist();
    }

    partial void OnConfirmBeforeRemoveChanged(bool value) => Persist();

    partial void OnMinimizeToTrayOnCloseChanged(bool value) => Persist();

    partial void OnIgdbClientIdChanged(string value) => Persist();
    partial void OnIgdbClientSecretChanged(string value) => Persist();
    partial void OnSteamGridDbApiKeyChanged(string value) => Persist();

    partial void OnSelectedWindowSizeChanged(WindowSizeOption value)
    {
        if (_suppressPersist)
            return;

        _windowService.ApplySize(value.Width, value.Height, value.Maximized);
        Persist();
    }

    private void LoadFromSettings()
    {
        _suppressPersist = true;
        try
        {
            var c = _settings.Current;
            SelectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == c.Theme) ?? ThemeOptions[0];
            SelectedTemperature = TemperatureOptions.FirstOrDefault(o => o.Value == c.Temperature) ?? TemperatureOptions[0];
            SelectedSort = SortOptions.FirstOrDefault(o => o.Value == c.Sort) ?? SortOptions[0];
            ConfirmBeforeRemove = c.ConfirmBeforeRemove;
            MinimizeToTrayOnClose = c.MinimizeToTrayOnClose;
            IgdbClientId = c.IgdbClientId ?? string.Empty;
            IgdbClientSecret = c.IgdbClientSecret ?? string.Empty;
            SteamGridDbApiKey = c.SteamGridDbApiKey ?? string.Empty;
            SelectedWindowSize =
                WindowSizeOptions.FirstOrDefault(o =>
                    o.Maximized == c.WindowMaximized &&
                    (c.WindowMaximized || (o.Width == c.WindowWidth && o.Height == c.WindowHeight)))
                ?? WindowSizeOptions[0];
            SelectedUiScale =
                UiScaleOptions.FirstOrDefault(o => Math.Abs(o.Scale - c.UiScale) < 0.001)
                ?? UiScaleOptions[1];
        }
        finally
        {
            _suppressPersist = false;
        }
    }

    private void Persist()
    {
        if (_suppressPersist)
            return;

        var updated = _settings.Current.Clone();
        updated.Theme = SelectedTheme.Value;
        updated.Temperature = SelectedTemperature.Value;
        updated.Sort = SelectedSort.Value;
        updated.ConfirmBeforeRemove = ConfirmBeforeRemove;
        updated.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        updated.UiScale = SelectedUiScale.Scale;
        updated.IgdbClientId = Blank(IgdbClientId);
        updated.IgdbClientSecret = Blank(IgdbClientSecret);
        updated.SteamGridDbApiKey = Blank(SteamGridDbApiKey);
        if (SelectedWindowSize.Maximized)
        {
            updated.WindowMaximized = true;
        }
        else
        {
            updated.WindowMaximized = false;
            updated.WindowWidth = SelectedWindowSize.Width;
            updated.WindowHeight = SelectedWindowSize.Height;
        }

        _ = PersistAsync(updated);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task PersistAsync(AppSettings settings)
    {
        try
        {
            await _settings.SaveAsync(settings).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not save settings");
            _dialogs.ShowError("Enregistrement impossible",
                $"Les paramètres n'ont pas pu être enregistrés :\n{ex.Message}");
        }
    }
}
