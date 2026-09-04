using Orbit.App.Services;
using Orbit.Core.Identification;
using Orbit.Core.Services;

namespace Orbit.App.ViewModels;

/// <summary>Backs the "Ajouter une application" form.</summary>
public sealed class AddAppViewModel : AppFormViewModel
{
    public AddAppViewModel(
        IExecutableInspector inspector,
        IDialogService dialogs,
        IAppIdentificationService identifier)
        : base(inspector, dialogs, identifier)
    {
    }

    public override string Title => "Ajouter une application";
    public override string PrimaryActionText => "Ajouter";
    public override bool SupportsDetectionShortcut => true;

    public NewAppRequest BuildRequest() => new()
    {
        ExecutablePath = ExecutablePath,
        Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
        Kind = Kind,
        Category = NullIfBlank(Category),
        Arguments = NullIfBlank(Arguments),
        WorkingDirectory = NullIfBlank(WorkingDirectory),
        Description = NullIfBlank(Description),
        IsFavorite = IsFavorite,
        Publisher = IdentifiedPublisher,
        Genre = IdentifiedGenre,
        CoverImagePath = IdentifiedCoverPath
    };

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
