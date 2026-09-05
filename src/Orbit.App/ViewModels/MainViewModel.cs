using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>Shell view-model: navigation, the shared search box, the status bar,
/// and the <see cref="ITileHost"/> implementation tiles call back into. All three
/// section views stay alive; navigation only flips visibility, so switching is
/// instant.</summary>
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

        if (Enum.TryParse<NavigationSection>(_settings.Current.LastSection, out var restored))
            _currentSection = restored;

        RaiseSectionFlags();
    }

    public HomeViewModel Home { get; }
    public LibraryViewModel Library { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private NavigationSection _currentSection = NavigationSection.Home;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private StatusSeverity _statusSeverity = StatusSeverity.Info;
    [ObservableProperty] private bool _isRefreshing;

    public bool IsHome => CurrentSection == NavigationSection.Home;
    public bool IsLibrary => CurrentSection == NavigationSection.Library;
    public bool IsSettings => CurrentSection == NavigationSection.Settings;

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
        RaiseSectionFlags();
        PersistLastSection(value);
    }

    partial void OnSearchTextChanged(string value) => Library.SearchText = value;

    private void RaiseSectionFlags()
    {
        OnPropertyChanged(nameof(IsHome));
        OnPropertyChanged(nameof(IsLibrary));
        OnPropertyChanged(nameof(IsSettings));
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
