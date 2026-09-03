using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.App.Infrastructure;
using Orbit.App.Services;
using Orbit.Core.Infrastructure;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

public sealed record ThemeOption(ThemePreference Value, string Label);

/// <summary>Backs the "Paramètres" page.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ThemeManager _themeManager;
    private readonly IDialogService _dialogs;
    private readonly ILibraryService _library;
    private readonly OrbitPaths _paths;
    private readonly ILogger _log;

    private bool _suppressPersist;

    public SettingsViewModel(
        ISettingsService settings,
        ThemeManager themeManager,
        IDialogService dialogs,
        ILibraryService library,
        OrbitPaths paths,
        ILogger log)
    {
        _settings = settings;
        _themeManager = themeManager;
        _dialogs = dialogs;
        _library = library;
        _paths = paths;
        _log = log.ForContext<SettingsViewModel>();

        _selectedTheme = ThemeOptions[0];
        _selectedSort = SortOptions[0];
        LoadFromSettings();
    }

    /// <summary>Set by <see cref="MainViewModel"/> so a data reset can refresh the shell.</summary>
    public ITileHost? Host { get; set; }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption(ThemePreference.System, "Système"),
        new ThemeOption(ThemePreference.Light, "Clair"),
        new ThemeOption(ThemePreference.Dark, "Sombre"),
    };

    public IReadOnlyList<SortOption> SortOptions { get; } = new[]
    {
        new SortOption(LibrarySort.Name, "Nom (A → Z)"),
        new SortOption(LibrarySort.RecentlyAdded, "Ajout récent"),
        new SortOption(LibrarySort.MostLaunched, "Plus lancés"),
        new SortOption(LibrarySort.LastLaunched, "Dernier lancement"),
    };

    [ObservableProperty] private ThemeOption _selectedTheme;
    [ObservableProperty] private SortOption _selectedSort;
    [ObservableProperty] private bool _confirmBeforeRemove = true;
    [ObservableProperty] private int _entryCount;

    public string VersionText
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "Orbit" : $"Orbit v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

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
        _themeManager.Apply(value.Value);
        Persist();
    }

    partial void OnSelectedSortChanged(SortOption value) => Persist();

    partial void OnConfirmBeforeRemoveChanged(bool value) => Persist();

    private void LoadFromSettings()
    {
        _suppressPersist = true;
        try
        {
            var current = _settings.Current;
            SelectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == current.Theme) ?? ThemeOptions[0];
            SelectedSort = SortOptions.FirstOrDefault(o => o.Value == current.Sort) ?? SortOptions[0];
            ConfirmBeforeRemove = current.ConfirmBeforeRemove;
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
        updated.Sort = SelectedSort.Value;
        updated.ConfirmBeforeRemove = ConfirmBeforeRemove;

        _ = PersistAsync(updated);
    }

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
