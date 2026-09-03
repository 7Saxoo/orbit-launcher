using Orbit.App.ViewModels;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.Services;

/// <summary>
/// The shared "pick an .exe and register it" interaction, used from both the
/// Home screen and the library toolbar. Shows the add form, calls the library
/// service and surfaces any error – returns the created entry, or null when the
/// user cancelled or the add failed.
/// </summary>
public sealed class AddAppFlow
{
    private readonly ILibraryService _library;
    private readonly IExecutableInspector _inspector;
    private readonly IDialogService _dialogs;
    private readonly ILogger _log;

    public AddAppFlow(ILibraryService library, IExecutableInspector inspector, IDialogService dialogs, ILogger log)
    {
        _library = library;
        _inspector = inspector;
        _dialogs = dialogs;
        _log = log.ForContext<AddAppFlow>();
    }

    public async Task<AppEntry?> RunAsync(AppKind? defaultKind = null)
    {
        var form = new AddAppViewModel(_inspector, _dialogs);
        if (defaultKind is { } kind)
            form.Kind = kind;

        if (!_dialogs.ShowAppForm(form))
            return null;

        try
        {
            return await _library.AddAsync(form.BuildRequest()).ConfigureAwait(true);
        }
        catch (LibraryException ex)
        {
            _dialogs.ShowError("Ajout impossible", ex.Message);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error while adding an entry");
            _dialogs.ShowError("Ajout impossible", $"Erreur inattendue :\n{ex.Message}");
        }

        return null;
    }
}
