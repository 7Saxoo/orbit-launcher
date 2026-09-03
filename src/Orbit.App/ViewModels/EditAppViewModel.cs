using Orbit.App.Services;
using Orbit.Core.Models;
using Orbit.Core.Services;

namespace Orbit.App.ViewModels;

/// <summary>Backs the "Modifier" form, seeded from an existing entry.</summary>
public sealed class EditAppViewModel : AppFormViewModel
{
    private readonly AppEntry _original;

    public EditAppViewModel(AppEntry entry, IExecutableInspector inspector, IDialogService dialogs)
        : base(inspector, dialogs)
    {
        _original = entry;

        ExecutablePath = entry.ExecutablePath;
        Name = entry.Name;
        Kind = entry.Kind;
        Category = entry.Category;
        Arguments = entry.Arguments ?? string.Empty;
        WorkingDirectory = entry.WorkingDirectory ?? string.Empty;
        Description = entry.Description ?? string.Empty;
        IsFavorite = entry.IsFavorite;
    }

    public override string Title => "Modifier l'application";
    public override string PrimaryActionText => "Enregistrer";

    /// <summary>Produces the updated entry to hand to the library service.</summary>
    public AppEntry BuildUpdatedEntry()
    {
        var updated = _original.Clone();
        updated.ExecutablePath = ExecutablePath.Trim();
        updated.Name = Name.Trim();
        updated.Kind = Kind;
        updated.Category = Category.Trim();
        updated.Arguments = NullIfBlank(Arguments);
        updated.WorkingDirectory = NullIfBlank(WorkingDirectory);
        updated.Description = NullIfBlank(Description);
        updated.IsFavorite = IsFavorite;
        return updated;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
