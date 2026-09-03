using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>Shell view-model: navigation, the shared search box, the status bar,
/// and the <see cref="ITileHost"/> implementation tiles call back into.</summary>
public sealed partial class MainViewModel : ObservableObject, ITileHost
{
    private readonly ISettingsService _settings;
    private readonly ILogger _log;

    public MainViewModel(
        HomeViewModel home,
        LibraryViewModel library,
        SettingsViewModel settings,
        AppTileContext tileContext,
        ISettingsService settingsService,
        ILogger log)
    {
        Home = home;
        Library = library;
        Settings = settings;
        _settings = settingsService;
        _log = log.ForContext<MainViewModel>();

        tileContext.Host = this;
        Settings.Host = this;
        _currentContent = Home;

        if (Enum.TryParse<NavigationSection>(_settings.Current.LastSection, out var restored))
            _currentSection = restored;

        UpdateContent();
    }

    public HomeViewModel Home { get; }
    public LibraryViewModel Library { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private NavigationSection _currentSection = NavigationSection.Home;
    [ObservableProperty] private object _currentContent;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private StatusSeverity _statusSeverity = StatusSeverity.Info;
    [ObservableProperty] private bool _isSearchVisible;
    [ObservableProperty] private bool _isRefreshing;

    [RelayCommand]
    private void Navigate(NavigationSection section) => CurrentSection = section;

    public async Task InitializeAsync()
    {
        _log.Information("Shell initialising");
        await RefreshAllAsync().ConfigureAwait(true);
        SetStatus("Prêt.", StatusSeverity.Info);
    }

    public async Task RefreshAllAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        try
        {
            await Library.RefreshAsync().ConfigureAwait(true);
            await Home.RefreshAsync().ConfigureAwait(true);
            await Settings.RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public void SetStatus(string message, StatusSeverity severity = StatusSeverity.Info)
    {
        StatusMessage = message;
        StatusSeverity = severity;
    }

    partial void OnCurrentSectionChanged(NavigationSection value)
    {
        UpdateContent();
        PersistLastSection(value);
    }

    partial void OnSearchTextChanged(string value) => Library.SearchText = value;

    private void UpdateContent()
    {
        IsSearchVisible = CurrentSection is
            NavigationSection.Library or NavigationSection.Games or
            NavigationSection.Applications or NavigationSection.Favorites;

        switch (CurrentSection)
        {
            case NavigationSection.Home:
                CurrentContent = Home;
                break;
            case NavigationSection.Settings:
                CurrentContent = Settings;
                break;
            case NavigationSection.Games:
                Library.FilterMode = LibraryFilterMode.Games;
                CurrentContent = Library;
                break;
            case NavigationSection.Applications:
                Library.FilterMode = LibraryFilterMode.Applications;
                CurrentContent = Library;
                break;
            case NavigationSection.Favorites:
                Library.FilterMode = LibraryFilterMode.Favorites;
                CurrentContent = Library;
                break;
            default:
                Library.FilterMode = LibraryFilterMode.All;
                CurrentContent = Library;
                break;
        }
    }

    private void PersistLastSection(NavigationSection section)
    {
        var updated = _settings.Current.Clone();
        updated.LastSection = section.ToString();
        _ = SafeSaveAsync(updated);
    }

    private async Task SafeSaveAsync(AppSettings settings)
    {
        try
        {
            await _settings.SaveAsync(settings).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not persist last navigation section");
        }
    }
}
