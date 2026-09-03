using Orbit.App.Services;
using Orbit.Core.Services;

namespace Orbit.App.ViewModels;

/// <summary>Backs the "Ajouter une application" form.</summary>
public sealed class AddAppViewModel : AppFormViewModel
{
    public AddAppViewModel(IExecutableInspector inspector, IDialogService dialogs)
        : base(inspector, dialogs)
    {
    }

    public override string Title => "Ajouter une application";
    public override string PrimaryActionText => "Ajouter";

    public NewAppRequest BuildRequest() => new()
    {
        ExecutablePath = ExecutablePath,
        Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
        Kind = Kind,
        Category = NullIfBlank(Category),
        Arguments = NullIfBlank(Arguments),
        WorkingDirectory = NullIfBlank(WorkingDirectory),
        Description = NullIfBlank(Description),
        IsFavorite = IsFavorite
    };

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
