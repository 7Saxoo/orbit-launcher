using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Detection;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>Drives the "Détection automatique" dialog: scan, review the list with
/// per-item checkboxes, import the selection.</summary>
public sealed partial class DetectionViewModel : ObservableObject
{
    private readonly IAppDetectionService _detection;
    private readonly ILibraryService _library;
    private readonly ILogger _log;

    public DetectionViewModel(IAppDetectionService detection, ILibraryService library, ILogger log)
    {
        _detection = detection;
        _library = library;
        _log = log.ForContext<DetectionViewModel>();

        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DetectionItemViewModel.Source)));
    }

    public ObservableCollection<DetectionItemViewModel> Items { get; } = new();
    public ICollectionView ItemsView { get; }

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasScanned;
    [ObservableProperty] private bool _isImporting;

    [ObservableProperty]
    private string _statusText =
        "Recherche les jeux et applications installés (Steam, Epic Games, programmes Windows).";

    /// <summary>Number of entries actually added on the last import.</summary>
    public int ImportedCount { get; private set; }

    public int SelectedCount => Items.Count(i => i.IsSelected);
    public bool CanImport => !IsScanning && !IsImporting && SelectedCount > 0;

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning)
            return;

        IsScanning = true;
        StatusText = "Analyse en cours…";
        DetachItemHandlers();
        Items.Clear();
        RaiseSelectionChanged();

        try
        {
            var result = await _detection.ScanAsync().ConfigureAwait(true);

            foreach (var app in result.NewItems)
            {
                var item = new DetectionItemViewModel(app);
                item.PropertyChanged += OnItemPropertyChanged;
                Items.Add(item);
            }

            HasScanned = true;
            StatusText = result.NewItems.Count == 0
                ? $"Aucune nouvelle application trouvée ({result.AlreadyInLibrary} déjà dans la bibliothèque)."
                : $"{result.NewItems.Count} application(s) trouvée(s), "
                  + $"{result.AlreadyInLibrary} déjà présente(s). Décochez ce que vous ne voulez pas.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Detection scan failed");
            StatusText = $"L'analyse a échoué : {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            RaiseSelectionChanged();
        }
    }

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void SelectNone() => SetAll(false);

    /// <summary>Imports the checked items. Returns the number added.</summary>
    public async Task<int> ImportSelectedAsync()
    {
        var chosen = Items.Where(i => i.IsSelected).Select(i => i.App).ToList();
        if (chosen.Count == 0)
            return 0;

        IsImporting = true;
        StatusText = "Import en cours…";
        RaiseSelectionChanged();
        try
        {
            ImportedCount = await _library.ImportAsync(chosen).ConfigureAwait(true);
            _log.Information("Imported {Count} detected apps", ImportedCount);
            return ImportedCount;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Import of detected apps failed");
            StatusText = $"L'import a échoué : {ex.Message}";
            return 0;
        }
        finally
        {
            IsImporting = false;
            RaiseSelectionChanged();
        }
    }

    private void SetAll(bool value)
    {
        foreach (var item in Items)
            item.IsSelected = value;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DetectionItemViewModel.IsSelected))
            RaiseSelectionChanged();
    }

    private void DetachItemHandlers()
    {
        foreach (var item in Items)
            item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CanImport));
    }

    partial void OnIsScanningChanged(bool value) => RaiseSelectionChanged();
    partial void OnIsImportingChanged(bool value) => RaiseSelectionChanged();
}
