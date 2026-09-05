using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.App.Infrastructure;
using Orbit.App.Services;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

public sealed record SortOption(LibrarySort Value, string Label);

/// <summary>
/// Backs the "Bibliothèque" section. One tile collection, filtered through a
/// single "Affichage" picker (Tout / Favoris / Jeux / Applications / a category)
/// plus the shared search box, and sorted through a <see cref="ListCollectionView"/>.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    public const string ViewAll = "Tout";
    public const string ViewFavorites = "Favoris";
    public const string ViewGames = "Jeux";
    public const string ViewApplications = "Applications";

    private readonly ILibraryService _library;
    private readonly AppTileContext _tileContext;
    private readonly AddAppFlow _addAppFlow;
    private readonly DetectionFlow _detectionFlow;
    private readonly RunningStateTicker _runningTicker;
    private readonly ILogger _log;
    private readonly ListCollectionView _view;
    private bool _trackingRunning;

    public LibraryViewModel(
        ILibraryService library,
        AppTileContext tileContext,
        AddAppFlow addAppFlow,
        DetectionFlow detectionFlow,
        RunningStateTicker runningTicker,
        ILogger log)
    {
        _library = library;
        _tileContext = tileContext;
        _addAppFlow = addAppFlow;
        _detectionFlow = detectionFlow;
        _runningTicker = runningTicker;
        _log = log.ForContext<LibraryViewModel>();

        _selectedSort = SortOptions[0];

        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(Items);
        _view.Filter = FilterTile;
        _view.CustomSort = BuildComparer(LibrarySort.Name);
        ((INotifyCollectionChanged)_view).CollectionChanged += (_, _) => NotifyCounts();
    }

    public ObservableCollection<AppTileViewModel> Items { get; } = new();
    public ICollectionView ItemsView => _view;

    public IReadOnlyList<SortOption> SortOptions { get; } = new[]
    {
        new SortOption(LibrarySort.Name, "Nom (A → Z)"),
        new SortOption(LibrarySort.RecentlyAdded, "Ajout récent"),
        new SortOption(LibrarySort.MostLaunched, "Plus lancés"),
        new SortOption(LibrarySort.LastLaunched, "Dernier lancement"),
    };

    /// <summary>Tout · Favoris · Jeux · Applications · &lt;each real category&gt;.</summary>
    public ObservableCollection<string> ViewOptions { get; } =
        new() { ViewAll, ViewFavorites, ViewGames, ViewApplications };

    [ObservableProperty] private string _selectedView = ViewAll;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private SortOption _selectedSort;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoadedOnce;
    [ObservableProperty] private bool _isSelectionMode;

    public int SelectedCount => Items.Count(t => t.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public int VisibleCount => _view.Count;
    public bool ShowEmptyState => HasLoadedOnce && !IsLoading && VisibleCount == 0;

    public string EmptyStateMessage =>
        !string.IsNullOrWhiteSpace(SearchText) ? $"Aucun résultat pour « {SearchText} »." :
        SelectedView == ViewFavorites ? "Aucun favori. Cliquez sur l'étoile d'une carte pour l'ajouter ici." :
        SelectedView == ViewGames ? "Aucun jeu. Ajoutez un .exe ou lancez l'analyse du PC." :
        SelectedView == ViewApplications ? "Aucune application. Ajoutez un .exe ou lancez l'analyse du PC." :
        SelectedView != ViewAll ? $"Aucun élément dans « {SelectedView} »." :
        "Votre bibliothèque est vide. Utilisez « Ajouter » ou « Analyser mon PC ».";

    public async Task RefreshAsync()
    {
        IsLoading = true;
        OnPropertyChanged(nameof(ShowEmptyState));
        try
        {
            var items = await _library.LoadAsync().ConfigureAwait(true);

            foreach (var old in Items)
                old.PropertyChanged -= OnTilePropertyChanged;
            Items.Clear();

            foreach (var item in items)
            {
                var tile = new AppTileViewModel(item, _tileContext) { SelectionMode = IsSelectionMode };
                tile.PropertyChanged += OnTilePropertyChanged;
                Items.Add(tile);
            }
            NotifySelection();
            RebuildViewOptions();
            HasLoadedOnce = true;

            if (!_trackingRunning)
            {
                _trackingRunning = true;
                _runningTicker.Track(() => Items);
            }

            _log.Debug("Library refreshed: {Count} entries", Items.Count);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load library");
            _tileContext.Dialogs.ShowError("Chargement impossible",
                $"La bibliothèque n'a pas pu être chargée :\n{ex.Message}");
        }
        finally
        {
            IsLoading = false;
            _view.Refresh();
            NotifyCounts();
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var defaultKind = SelectedView == ViewGames ? AppKind.Game : (AppKind?)null;

        var outcome = await _addAppFlow.RunAsync(defaultKind).ConfigureAwait(true);
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
    private Task Reload() => RefreshAsync();

    [RelayCommand]
    private async Task ScanForAppsAsync()
    {
        var imported = _detectionFlow.Run();
        if (imported <= 0)
            return;

        _tileContext.Host.SetStatus($"{imported} application(s) importée(s).", StatusSeverity.Success);
        await _tileContext.Host.RefreshAllAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void ToggleSelectionMode() => IsSelectionMode = !IsSelectionMode;

    [RelayCommand]
    private void SelectAllVisible() => SetVisibleSelection(true);

    [RelayCommand]
    private void SelectNone() => SetVisibleSelection(false);

    private void SetVisibleSelection(bool selected)
    {
        foreach (var tile in _view.Cast<AppTileViewModel>())
            tile.IsSelected = selected;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        var targets = Items.Where(t => t.IsSelected).Select(t => t.Id).ToList();
        if (targets.Count == 0)
            return;

        if (!_tileContext.Dialogs.Confirm("Supprimer la sélection",
                $"Retirer {targets.Count} application(s) de la bibliothèque ?\n\n" +
                "Les fichiers .exe d'origine ne sont pas supprimés."))
        {
            return;
        }

        try
        {
            var removed = await _library.RemoveManyAsync(targets).ConfigureAwait(true);
            IsSelectionMode = false;
            _tileContext.Host.SetStatus($"{removed} application(s) retirée(s).", StatusSeverity.Warning);
            await _tileContext.Host.RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Bulk delete failed");
            _tileContext.Dialogs.ShowError("Suppression impossible", ex.Message);
        }
    }

    partial void OnIsSelectionModeChanged(bool value)
    {
        foreach (var tile in Items)
        {
            tile.SelectionMode = value;
            if (!value)
                tile.IsSelected = false;
        }
        NotifySelection();
    }

    partial void OnSearchTextChanged(string value)
    {
        _view.Refresh();
        NotifyCounts();
    }

    partial void OnSelectedViewChanged(string value)
    {
        _view.Refresh();
        NotifyCounts();
    }

    partial void OnSelectedSortChanged(SortOption value)
    {
        _view.CustomSort = BuildComparer(value?.Value ?? LibrarySort.Name);
        _view.Refresh();
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));

    private void OnTilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppTileViewModel.IsSelected))
            NotifySelection();
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private bool FilterTile(object obj)
    {
        if (obj is not AppTileViewModel tile)
            return false;

        var matchesView = SelectedView switch
        {
            ViewAll => true,
            ViewFavorites => tile.IsFavorite,
            ViewGames => tile.Entry.Kind == AppKind.Game,
            ViewApplications => tile.Entry.Kind == AppKind.Application,
            _ => string.Equals(tile.Entry.Category, SelectedView, StringComparison.CurrentCultureIgnoreCase),
        };
        if (!matchesView)
            return false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var needle = SearchText.Trim();
        return Contains(tile.Entry.Name, needle)
            || Contains(tile.Entry.Category, needle)
            || Contains(tile.Entry.Description, needle);
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) &&
        haystack.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

    private void RebuildViewOptions()
    {
        var previous = SelectedView;

        var categories = Items
            .Select(t => t.Entry.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ViewOptions.Clear();
        ViewOptions.Add(ViewAll);
        ViewOptions.Add(ViewFavorites);
        ViewOptions.Add(ViewGames);
        ViewOptions.Add(ViewApplications);
        foreach (var category in categories)
            ViewOptions.Add(category);

        SelectedView = ViewOptions.Contains(previous, StringComparer.CurrentCultureIgnoreCase)
            ? previous
            : ViewAll;
    }

    private static System.Collections.IComparer BuildComparer(LibrarySort sort) => new TileComparer(sort);

    private sealed class TileComparer(LibrarySort sort) : System.Collections.IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not AppTileViewModel a || y is not AppTileViewModel b)
                return 0;

            return sort switch
            {
                LibrarySort.RecentlyAdded => b.Entry.DateAdded.CompareTo(a.Entry.DateAdded),
                LibrarySort.MostLaunched => b.Entry.LaunchCount.CompareTo(a.Entry.LaunchCount),
                LibrarySort.LastLaunched => Nullable.Compare(b.Entry.LastLaunchedAt, a.Entry.LastLaunchedAt),
                _ => string.Compare(a.Entry.Name, b.Entry.Name, StringComparison.CurrentCultureIgnoreCase)
            };
        }
    }
}
