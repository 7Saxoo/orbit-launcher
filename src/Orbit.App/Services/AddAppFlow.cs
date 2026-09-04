using Orbit.App.ViewModels;
using Orbit.Core.Identification;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.Services;

/// <summary>Result of the add interaction: an entry that was created, and/or a
/// count of apps imported because the user switched to auto-detection.</summary>
public sealed record AddOutcome(AppEntry? Added, int Detected)
{
    public static readonly AddOutcome Nothing = new(null, 0);
    public bool ChangedLibrary => Added is not null || Detected > 0;
}

/// <summary>
/// The shared "pick an .exe and register it" interaction. Shows the add form
/// (with automatic identification), or hands off to the PC scan when the user
/// asks for it.
/// </summary>
public sealed class AddAppFlow
{
    private readonly ILibraryService _library;
    private readonly IExecutableInspector _inspector;
    private readonly IAppIdentificationService _identifier;
    private readonly IDialogService _dialogs;
    private readonly DetectionFlow _detectionFlow;
    private readonly ILogger _log;

    public AddAppFlow(
        ILibraryService library,
        IExecutableInspector inspector,
        IAppIdentificationService identifier,
        IDialogService dialogs,
        DetectionFlow detectionFlow,
        ILogger log)
    {
        _library = library;
        _inspector = inspector;
        _identifier = identifier;
        _dialogs = dialogs;
        _detectionFlow = detectionFlow;
        _log = log.ForContext<AddAppFlow>();
    }

    public async Task<AddOutcome> RunAsync(AppKind? defaultKind = null)
    {
        var form = new AddAppViewModel(_inspector, _dialogs, _identifier);
        if (defaultKind is { } kind)
            form.Kind = kind;

        var saved = _dialogs.ShowAppForm(form);

        if (form.DetectionRequested)
            return new AddOutcome(null, _detectionFlow.Run());

        if (!saved)
            return AddOutcome.Nothing;

        try
        {
            var entry = await _library.AddAsync(form.BuildRequest()).ConfigureAwait(true);
            return new AddOutcome(entry, 0);
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

        return AddOutcome.Nothing;
    }
}
