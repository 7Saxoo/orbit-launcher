using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Models;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>One card in the library / home grids. Clean surface: a primary
/// action, a favourite star and a ⋯ button that opens the per-app settings.</summary>
public sealed partial class AppTileViewModel : ObservableObject
{
    private readonly AppTileContext _ctx;
    private readonly ILogger _log;

    public AppTileViewModel(LibraryItem item, AppTileContext ctx)
    {
        _ctx = ctx;
        _log = ctx.Log.ForContext<AppTileViewModel>();
        Entry = item.Entry;
        _availability = item.Availability;
        _isFavorite = item.Entry.IsFavorite;
    }

    public AppEntry Entry { get; }

    public Guid Id => Entry.Id;
    public string Name => Entry.Name;
    public string? IconPath => Entry.IconCachePath;
    public string KindLabel => Entry.Kind == AppKind.Game ? "Jeu" : "Application";
    public string CategoryLabel => string.IsNullOrWhiteSpace(Entry.Category) ? "Sans catégorie" : Entry.Category;

    private bool CanLaunchDespiteMissing => !string.IsNullOrWhiteSpace(Entry.LaunchUri);

    public string PrimaryActionLabel =>
        Availability == AppAvailability.Missing && !CanLaunchDespiteMissing ? "Fichier introuvable" :
        IsRunning ? "▶  Relancer" :
        Entry.Kind == AppKind.Game ? "▶  Jouer" : "▶  Ouvrir";

    /// <summary>"Jeu · En cours" while the process is up, otherwise just the kind.</summary>
    public string SubtitleLabel => IsRunning ? $"{KindLabel}  ·  En cours" : KindLabel;

    [ObservableProperty] private AppAvailability _availability;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRunning;

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(SubtitleLabel));
        OnPropertyChanged(nameof(PrimaryActionLabel));
    }

    /// <summary>Called by the owning view-model on a timer with one process snapshot.</summary>
    public void UpdateRunningState(IReadOnlySet<string> runningImageNames)
    {
        var path = Entry.ExecutablePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            IsRunning = false;
            return;
        }

        IsRunning = runningImageNames.Contains(Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>Set by the library VM when bulk-selection mode is active.</summary>
    [ObservableProperty] private bool _selectionMode;

    /// <summary>Ticked in bulk-selection mode. Watched by the library VM.</summary>
    [ObservableProperty] private bool _isSelected;

    [RelayCommand]
    private void ToggleSelected() => IsSelected = !IsSelected;

    public bool IsMissing => Availability != AppAvailability.Available && !CanLaunchDespiteMissing;

    partial void OnAvailabilityChanged(AppAvailability value)
    {
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(PrimaryActionLabel));
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task LaunchAsync()
    {
        if (Availability == AppAvailability.Missing && !CanLaunchDespiteMissing)
        {
            OpenSettings();
            return;
        }

        if (_ctx.Library.IsRunning(Entry) &&
            !_ctx.Dialogs.Confirm("Déjà en cours d'exécution",
                $"« {Name} » semble déjà ouvert. Le lancer à nouveau ?"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var outcome = await _ctx.Library.LaunchAsync(Id).ConfigureAwait(true);
            if (outcome.Succeeded)
            {
                _ctx.Host.SetStatus(outcome.Message, StatusSeverity.Success);
                await _ctx.Host.RefreshAllAsync().ConfigureAwait(true);
            }
            else
            {
                _ctx.Host.SetStatus(outcome.Message, StatusSeverity.Error);
                _ctx.Dialogs.ShowError("Lancement impossible", outcome.Message);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error launching {Id}", Id);
            _ctx.Dialogs.ShowError("Lancement impossible", $"Erreur inattendue :\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task ToggleFavoriteAsync()
    {
        try
        {
            var updated = await _ctx.Library.SetFavoriteAsync(Id, !IsFavorite).ConfigureAwait(true);
            IsFavorite = updated.IsFavorite;
            await _ctx.Host.RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not toggle favourite for {Id}", Id);
            _ctx.Dialogs.ShowError("Action impossible", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task OpenSettingsAsync()
    {
        var vm = new AppSettingsViewModel(Entry, _ctx.Library, _ctx.Dialogs, _ctx.Inspector, _ctx.Settings, _ctx.Log);
        _ctx.Dialogs.ShowAppSettings(vm);

        if (vm.ChangesMade)
            await _ctx.Host.RefreshAllAsync().ConfigureAwait(true);
    }

    private void OpenSettings() => OpenSettingsCommand.Execute(null);

    private bool CanInteract() => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        LaunchCommand.NotifyCanExecuteChanged();
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
    }
}
