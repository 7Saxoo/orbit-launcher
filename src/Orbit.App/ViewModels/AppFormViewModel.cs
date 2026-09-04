using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.App.Services;
using Orbit.Core.Identification;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Orbit.Core.Services;

namespace Orbit.App.ViewModels;

/// <summary>Shared state and validation for the "add" form.</summary>
public abstract partial class AppFormViewModel : ObservableValidator
{
    private readonly IExecutableInspector _inspector;
    private readonly IDialogService _dialogs;
    private readonly IAppIdentificationService? _identifier;

    protected AppFormViewModel(
        IExecutableInspector inspector,
        IDialogService dialogs,
        IAppIdentificationService? identifier = null)
    {
        _inspector = inspector;
        _dialogs = dialogs;
        _identifier = identifier;
    }

    public abstract string Title { get; }
    public abstract string PrimaryActionText { get; }

    /// <summary>Add mode shows a "detect automatically" shortcut; edit mode doesn't.</summary>
    public virtual bool SupportsDetectionShortcut => false;

    /// <summary>Set when the user chose the "detect automatically" shortcut instead of filling the form.</summary>
    public bool DetectionRequested { get; private set; }

    /// <summary>Raised when the view should close (used by the detection shortcut).</summary>
    public event EventHandler? CloseRequested;

    // Captured from the identification step for BuildRequest().
    public string? IdentifiedPublisher { get; private set; }
    public string? IdentifiedGenre { get; private set; }
    public string? IdentifiedCoverPath { get; private set; }

    public IReadOnlyList<AppKind> KindOptions { get; } = new[] { AppKind.Application, AppKind.Game };

    public ObservableCollection<string> CategorySuggestions { get; } = new()
    {
        "Jeux", "Bureautique", "Développement", "Multimédia", "Communication",
        "Utilitaires", "Inconnu", "Autre"
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

    [ObservableProperty] private AppKind _kind = AppKind.Application;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _arguments = string.Empty;
    [ObservableProperty] private string _workingDirectory = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isFavorite;

    /// <summary>Non-blocking hint shown under the path field.</summary>
    [ObservableProperty] private string? _pathHint;

    /// <summary>Result of the automatic identification, shown under the path field.</summary>
    [ObservableProperty] private string? _identificationSummary;

    [ObservableProperty] private bool _isIdentifying;

    [RelayCommand]
    private void UseDetection()
    {
        DetectionRequested = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task BrowseExecutableAsync()
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

        await IdentifyAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task IdentifyAsync()
    {
        if (_identifier is null || IsIdentifying)
            return;

        var path = PathHelper.Normalize(ExecutablePath);
        if (!PathHelper.HasExecutableExtension(path) || !File.Exists(path))
            return;

        IsIdentifying = true;
        IdentificationSummary = "Analyse du fichier…";
        try
        {
            var id = await _identifier.IdentifyAsync(path).ConfigureAwait(true);

            IdentifiedPublisher = id.Publisher;
            IdentifiedGenre = id.Genre;
            IdentifiedCoverPath = id.CoverImagePath;

            if (id.IsReliable)
            {
                Kind = id.ToAppKind();
                if (!string.IsNullOrWhiteSpace(id.Name))
                    Name = id.Name!;
                if (string.IsNullOrWhiteSpace(Category))
                    Category = id.SuggestedCategory;

                var kindLabel = id.Kind == IdentificationKind.Game ? "Jeu" : "Application";
                IdentificationSummary =
                    $"✓ {kindLabel} — {id.Name}  ·  {id.Source}  ·  confiance {id.Confidence:P0}";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Category))
                    Category = "Inconnu";
                IdentificationSummary =
                    $"? Non reconnu de façon fiable ({id.Source}). Choisissez le type manuellement.";
            }
        }
        catch (Exception ex)
        {
            IdentificationSummary = $"Identification impossible : {ex.Message}";
        }
        finally
        {
            IsIdentifying = false;
        }
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
