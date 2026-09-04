using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.App.Services;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

public sealed record SortOption(LibrarySort Value, string Label);

/// <summary>
/// Shared view-model for the Bibliothèque / Jeux / Applications / Favoris
/// sections. A single tile collection is filtered and sorted through a
/// <see cref="ListCollectionView"/>; navigation just flips <see cref="FilterMode"/>.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryService _library;
    private readonly AppTileContext _tileContext;
    private readonly AddAppFlow _addAppFlow;
    private readonly DetectionFlow _detectionFlow;
    private readonly Orbit.App.Infrastructure.RunningStateTicker _runningTicker;
    private readonly ILogger _log;
    private readonly ListCollectionView _view;
    private bool _trackingRunning;

    public LibraryViewModel(
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

    public ObservableCollection<string> Categories { get; } = new() { AllCategories };

    private const string AllCategories = "Toutes les catégories";

    [ObservableProperty] private LibraryFilterMode _filterMode = LibraryFilterMode.All;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private SortOption _selectedSort;
    [ObservableProperty] private string _selectedCategory = AllCategories;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoadedOnce;
    [ObservableProperty] private bool _isSelectionMode;

    public int SelectedCount => Items.Count(t => t.IsSelected);
    public bool HasSelection => SelectedCount > 0;

    public int VisibleCount => _view.Count;

    public bool ShowEmptyState => HasLoadedOnce && !IsLoading && VisibleCount == 0;

    public string Heading => FilterMode switch
    {
        LibraryFilterMode.Games => "Jeux",
        LibraryFilterMode.Applications => "Applications",
        LibraryFilterMode.Favorites => "Favoris",
        _ => "Bibliothèque"
    };

    public string EmptyStateMessage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
                return $"Aucun résultat pour « {SearchText} ».";

            return FilterMode switch
            {
                LibraryFilterMode.Games => "Aucun jeu pour le moment. Ajoutez un .exe pour commencer.",
                LibraryFilterMode.Applications => "Aucune application pour le moment. Ajoutez un .exe pour commencer.",
                LibraryFilterMode.Favorites => "Aucun favori. Cliquez sur l'étoile d'une carte pour l'ajouter ici.",
                _ => "Votre bibliothèque est vide. Utilisez « Ajouter » pour enregistrer un .exe."
            };
        }
    }

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

            RebuildCategories();
            HasLoadedOnce = true;

            if (!_trackingRunning)
            {
                _trackingRunning = true;
                _runningTicker.Track(() => Items);
            }

            _log.Debug("Library view refreshed: {Count} entries", Items.Count);
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
        var defaultKind = FilterMode == LibraryFilterMode.Games
            ? Orbit.Core.Models.AppKind.Game
            : (Orbit.Core.Models.AppKind?)null;

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

    partial void OnSearchTextChanged(string value) => _view.Refresh();

    partial void OnFilterModeChanged(LibraryFilterMode value)
    {
        _view.Refresh();
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(EmptyStateMessage));
        NotifyCounts();
    }

    partial void OnSelectedCategoryChanged(string value) => _view.Refresh();

    partial void OnSelectedSortChanged(SortOption value)
    {
        _view.CustomSort = BuildComparer(value?.Value ?? LibrarySort.Name);
        NotifyCounts();
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));

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

        var matchesMode = FilterMode switch
        {
            LibraryFilterMode.Games => tile.Entry.Kind == Orbit.Core.Models.AppKind.Game,
            LibraryFilterMode.Applications => tile.Entry.Kind == Orbit.Core.Models.AppKind.Application,
            LibraryFilterMode.Favorites => tile.IsFavorite,
            _ => true
        };
        if (!matchesMode)
            return false;

        if (SelectedCategory != AllCategories &&
            !string.Equals(tile.Entry.Category, SelectedCategory, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

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

    private void RebuildCategories()
    {
        var previous = SelectedCategory;

        var distinct = Items
            .Select(t => t.Entry.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Categories.Clear();
        Categories.Add(AllCategories);
        foreach (var category in distinct)
            Categories.Add(category);

        SelectedCategory = Categories.Contains(previous) ? previous : AllCategories;
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
