using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.App.Services;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>The "Accueil" dashboard: headline counts plus recently added and
/// most launched shortcuts.</summary>
public sealed partial class HomeViewModel : ObservableObject
{
    private const int RailSize = 6;

    private readonly ILibraryService _library;
    private readonly AppTileContext _tileContext;
    private readonly AddAppFlow _addAppFlow;
    private readonly DetectionFlow _detectionFlow;
    private readonly Orbit.App.Infrastructure.RunningStateTicker _runningTicker;
    private readonly ILogger _log;
    private bool _trackingRunning;

    public HomeViewModel(
        ILibraryService library,
        AppTileContext tileContext,
        AddAppFlow addAppFlow,
        DetectionFlow detectionFlow,
        Orbit.App.Infrastructure.RunningStateTicker runningTicker,
        ILogger log)
    {
        _library = library;
        _tileContext = tileContext;
        _addAppFlow = addAppFlow;
        _detectionFlow = detectionFlow;
        _runningTicker = runningTicker;
        _log = log.ForContext<HomeViewModel>();
    }

    public ObservableCollection<AppTileViewModel> RecentlyAdded { get; } = new();
    public ObservableCollection<AppTileViewModel> MostLaunched { get; } = new();

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _gamesCount;
    [ObservableProperty] private int _applicationsCount;
    [ObservableProperty] private int _favoritesCount;
    [ObservableProperty] private int _missingCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoadedOnce;

    public bool IsEmpty => HasLoadedOnce && !IsLoading && TotalCount == 0;
    public bool HasMostLaunched => MostLaunched.Count > 0;

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var items = (await _library.LoadAsync().ConfigureAwait(true)).ToList();

            TotalCount = items.Count;
            GamesCount = items.Count(i => i.Entry.Kind == AppKind.Game);
            ApplicationsCount = items.Count(i => i.Entry.Kind == AppKind.Application);
            FavoritesCount = items.Count(i => i.Entry.IsFavorite);
            MissingCount = items.Count(i => i.Availability != AppAvailability.Available);

            Fill(RecentlyAdded, items
                .OrderByDescending(i => i.Entry.DateAdded)
                .Take(RailSize));

            Fill(MostLaunched, items
                .Where(i => i.Entry.LaunchCount > 0)
                .OrderByDescending(i => i.Entry.LaunchCount)
                .ThenByDescending(i => i.Entry.LastLaunchedAt)
                .Take(RailSize));

            HasLoadedOnce = true;

            if (!_trackingRunning)
            {
                _trackingRunning = true;
                _runningTicker.Track(() => RecentlyAdded.Concat(MostLaunched));
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to refresh home dashboard");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasMostLaunched));
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var outcome = await _addAppFlow.RunAsync().ConfigureAwait(true);
        if (!outcome.ChangedLibrary)
            return;

        _tileContext.Host.SetStatus(
            outcome.Added is { } entry
                ? $"« {entry.Name} » a été ajouté."
                : $"{outcome.Detected} application(s) importée(s).",
            StatusSeverity.Success);
        await _tileContext.Host.RefreshAllAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ScanForAppsAsync()
    {
        var imported = _detectionFlow.Run();
        if (imported <= 0)
            return;

        _tileContext.Host.SetStatus($"{imported} application(s) importée(s).", StatusSeverity.Success);
        await _tileContext.Host.RefreshAllAsync().ConfigureAwait(true);
    }

    private void Fill(ObservableCollection<AppTileViewModel> target, IEnumerable<LibraryItem> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(new AppTileViewModel(item, _tileContext));
    }
}
