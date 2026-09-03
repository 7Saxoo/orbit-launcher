using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.App.Services;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Orbit.Core.Services;

namespace Orbit.App.ViewModels;

/// <summary>Shared state and validation for the "add" and "edit" forms.</summary>
public abstract partial class AppFormViewModel : ObservableValidator
{
    private readonly IExecutableInspector _inspector;
    private readonly IDialogService _dialogs;

    protected AppFormViewModel(IExecutableInspector inspector, IDialogService dialogs)
    {
        _inspector = inspector;
        _dialogs = dialogs;
    }

    public abstract string Title { get; }
    public abstract string PrimaryActionText { get; }

    public IReadOnlyList<AppKind> KindOptions { get; } = new[] { AppKind.Application, AppKind.Game };

    public ObservableCollection<string> CategorySuggestions { get; } = new()
    {
        "Jeux", "Bureautique", "Développement", "Multimédia", "Communication", "Utilitaires", "Autre"
    };

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Sélectionnez un fichier .exe.")]
    [CustomValidation(typeof(AppFormViewModel), nameof(ValidateExecutablePath))]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(120, ErrorMessage = "120 caractères maximum.")]
    private string _name = string.Empty;

    [ObservableProperty]
    private AppKind _kind = AppKind.Application;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isFavorite;

    /// <summary>Non-blocking hint shown under the path field (e.g. "fichier introuvable").</summary>
    [ObservableProperty]
    private string? _pathHint;

    [RelayCommand]
    private void BrowseExecutable()
    {
        var start = PathHelper.GetContainingDirectory(ExecutablePath);
        var picked = _dialogs.PickExecutable(start);
        if (picked is null)
            return;

        var info = _inspector.Inspect(picked);
        ExecutablePath = info.NormalizedPath;

        if (string.IsNullOrWhiteSpace(Name) && info.SuggestedName is { } suggestion)
            Name = suggestion;

        if (string.IsNullOrWhiteSpace(Description) && info.FileDescription is { } fileDescription)
            Description = fileDescription;

        PathHint = info.Availability switch
        {
            AppAvailability.Missing => "Ce fichier est introuvable.",
            AppAvailability.Invalid => "Ce n'est pas un fichier .exe.",
            _ => info.CompanyName is { } company ? $"Éditeur : {company}" : null
        };
    }

    [RelayCommand]
    private void BrowseWorkingDirectory()
    {
        var start = WorkingDirectory.Length > 0
            ? WorkingDirectory
            : PathHelper.GetContainingDirectory(ExecutablePath);
        var picked = _dialogs.PickFolder(start);
        if (picked is not null)
            WorkingDirectory = picked;
    }

    /// <summary>Runs full validation. Returns true when the form can be submitted.</summary>
    public bool Validate()
    {
        ValidateAllProperties();
        return !HasErrors;
    }

    public static ValidationResult? ValidateExecutablePath(string? path, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Success; // handled by [Required]

        if (!PathHelper.HasExecutableExtension(path))
            return new ValidationResult("Le fichier doit avoir l'extension .exe.");

        return File.Exists(PathHelper.Normalize(path))
            ? ValidationResult.Success
            : new ValidationResult("Ce fichier n'existe pas (vous pourrez corriger le chemin plus tard).");
    }
}
