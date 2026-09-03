using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>One card in the library / home grids. Owns the per-entry actions
/// (launch, favourite, edit, remove, fix path, reveal).</summary>
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
    public string LaunchCountLabel =>
        Entry.LaunchCount == 0 ? "Jamais lancé" : $"{Entry.LaunchCount} lancement{(Entry.LaunchCount > 1 ? "s" : "")}";
    public DateTimeOffset? LastLaunchedAt => Entry.LastLaunchedAt;
    public string? Description => Entry.Description;

    [ObservableProperty]
    private AppAvailability _availability;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsMissing => Availability != AppAvailability.Available;

    partial void OnAvailabilityChanged(AppAvailability value) => OnPropertyChanged(nameof(IsMissing));

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task LaunchAsync()
    {
        if (Availability == AppAvailability.Missing)
        {
            PromptToFixMissingFile();
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
            _ctx.Dialogs.ShowError("Lancement impossible", $"Une erreur inattendue est survenue :\n{ex.Message}");
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
    private async Task EditAsync()
    {
        var form = new EditAppViewModel(Entry, _ctx.Inspector, _ctx.Dialogs);
        if (!_ctx.Dialogs.ShowAppForm(form))
            return;

        try
        {
            await _ctx.Library.UpdateAsync(form.BuildUpdatedEntry()).ConfigureAwait(true);
            _ctx.Host.SetStatus($"« {form.Name} » a été mis à jour.", StatusSeverity.Success);
            await _ctx.Host.RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not update {Id}", Id);
            _ctx.Dialogs.ShowError("Enregistrement impossible", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task RemoveAsync()
    {
        if (_ctx.Settings.Current.ConfirmBeforeRemove &&
            !_ctx.Dialogs.Confirm("Retirer de la bibliothèque",
                $"Retirer « {Name} » de la bibliothèque ?\n\nLe fichier .exe d'origine ne sera pas supprimé."))
        {
            return;
        }

        try
        {
            await _ctx.Library.RemoveAsync(Id).ConfigureAwait(true);
            _ctx.Host.SetStatus($"« {Name} » a été retiré (le fichier .exe est intact).", StatusSeverity.Info);
            await _ctx.Host.RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not remove {Id}", Id);
            _ctx.Dialogs.ShowError("Suppression impossible", ex.Message);
        }
    }

    [RelayCommand]
    private void RevealInExplorer() => _ctx.Dialogs.RevealInExplorer(Entry.ExecutablePath);

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private Task FixPathAsync()
    {
        PromptToFixMissingFile();
        return Task.CompletedTask;
    }

    private bool CanInteract() => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        LaunchCommand.NotifyCanExecuteChanged();
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        FixPathCommand.NotifyCanExecuteChanged();
    }

    private async void PromptToFixMissingFile()
    {
        var picked = _ctx.Dialogs.PickExecutable(
            Orbit.Core.Infrastructure.PathHelper.GetContainingDirectory(Entry.ExecutablePath));
        if (picked is null)
            return;

        try
        {
            var updated = Entry.Clone();
            updated.ExecutablePath = picked;
            await _ctx.Library.UpdateAsync(updated).ConfigureAwait(true);
            _ctx.Host.SetStatus($"Chemin de « {Name} » mis à jour.", StatusSeverity.Success);
            await _ctx.Host.RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not fix path for {Id}", Id);
            _ctx.Dialogs.ShowError("Mise à jour impossible", ex.Message);
        }
    }
}
