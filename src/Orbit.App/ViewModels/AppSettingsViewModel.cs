using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.App.Services;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>
/// "Paramètres de l'application" — the single place to edit an entry, tune its
/// launch options, reveal it, launch it or remove it. Opened from a tile's ⋯ menu.
/// </summary>
public sealed partial class AppSettingsViewModel : ObservableValidator
{
    private readonly AppEntry _original;
    private readonly ILibraryService _library;
    private readonly IDialogService _dialogs;
    private readonly IExecutableInspector _inspector;
    private readonly ISettingsService _appSettings;
    private readonly ILogger _log;

    public AppSettingsViewModel(
        AppEntry entry,
        ILibraryService library,
        IDialogService dialogs,
        IExecutableInspector inspector,
        ISettingsService appSettings,
        ILogger log)
    {
        _original = entry;
        _library = library;
        _dialogs = dialogs;
        _inspector = inspector;
        _appSettings = appSettings;
        _log = log.ForContext<AppSettingsViewModel>();

        _name = entry.Name;
        _kind = entry.Kind;
        _category = entry.Category;
        _description = entry.Description ?? string.Empty;
        _executablePath = entry.ExecutablePath;
        _workingDirectory = entry.WorkingDirectory ?? string.Empty;
        _arguments = entry.Arguments ?? string.Empty;
        _runAsAdmin = entry.RunAsAdmin;
        _javaMemoryText = entry.JavaMaxMemoryMb?.ToString() ?? string.Empty;
        _launchUri = entry.LaunchUri ?? string.Empty;
    }

    public Guid Id => _original.Id;
    public string? IconPath => _original.IconCachePath;
    public string HeaderKind => Kind == AppKind.Game ? "Jeu" : "Application";

    /// <summary>True when the library was modified (save / remove / launch stats).</summary>
    public bool ChangesMade { get; private set; }

    public event EventHandler? RequestClose;

    public IReadOnlyList<AppKind> KindOptions { get; } = new[] { AppKind.Application, AppKind.Game };

    public ObservableCollection<string> CategorySuggestions { get; } = new()
    {
        "Jeux", "Bureautique", "Développement", "Multimédia", "Communication", "Utilitaires", "Autre"
    };

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(120, ErrorMessage = "120 caractères maximum.")]
    private string _name = string.Empty;

    [ObservableProperty] private AppKind _kind;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Sélectionnez un fichier .exe.")]
    [CustomValidation(typeof(AppSettingsViewModel), nameof(ValidateExe))]
    private string _executablePath = string.Empty;

    [ObservableProperty] private string _workingDirectory = string.Empty;
    [ObservableProperty] private string _arguments = string.Empty;
    [ObservableProperty] private bool _runAsAdmin;

    /// <summary>Overrides the executable at launch (e.g. steam://rungameid/…).</summary>
    [ObservableProperty] private string _launchUri = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AppSettingsViewModel), nameof(ValidateJavaMemory))]
    private string _javaMemoryText = string.Empty;

    public string LocationPath => PathHelper.Normalize(ExecutablePath);

    [RelayCommand]
    private void BrowseExecutable()
    {
        var picked = _dialogs.PickExecutable(PathHelper.GetContainingDirectory(ExecutablePath));
        if (picked is not null)
            ExecutablePath = _inspector.Inspect(picked).NormalizedPath;
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

    [RelayCommand]
    private void OpenLocation() => _dialogs.RevealInExplorer(LocationPath);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
            return;

        try
        {
            await _library.UpdateAsync(BuildEntry()).ConfigureAwait(true);
            ChangesMade = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not save app settings for {Id}", Id);
            _dialogs.ShowError("Enregistrement impossible", ex.Message);
        }
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        ValidateAllProperties();
        if (!HasErrors)
        {
            try
            {
                await _library.UpdateAsync(BuildEntry()).ConfigureAwait(true);
                ChangesMade = true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Could not persist before launch");
            }
        }

        var outcome = await _library.LaunchAsync(Id).ConfigureAwait(true);
        ChangesMade = true;
        if (!outcome.Succeeded)
            _dialogs.ShowError("Lancement impossible", outcome.Message);

        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (!_dialogs.Confirm("Supprimer de la bibliothèque",
                $"Retirer « {Name} » de la bibliothèque ?\n\nLe fichier .exe d'origine ne sera pas supprimé."))
        {
            return;
        }

        try
        {
            await _library.RemoveAsync(Id).ConfigureAwait(true);
            ChangesMade = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not remove {Id}", Id);
            _dialogs.ShowError("Suppression impossible", ex.Message);
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    private AppEntry BuildEntry()
    {
        var e = _original.Clone();
        e.Name = Name.Trim();
        e.Kind = Kind;
        e.Category = Category.Trim();
        e.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        e.ExecutablePath = ExecutablePath.Trim();
        e.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory.Trim();
        e.Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim();
        e.RunAsAdmin = RunAsAdmin;
        e.JavaMaxMemoryMb = int.TryParse(JavaMemoryText, out var mb) && mb > 0 ? mb : null;
        e.LaunchUri = string.IsNullOrWhiteSpace(LaunchUri) ? null : LaunchUri.Trim();
        return e;
    }

    public static ValidationResult? ValidateExe(string? path, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Success;
        if (!PathHelper.HasExecutableExtension(path))
            return new ValidationResult("Le fichier doit avoir l'extension .exe.");
        return File.Exists(PathHelper.Normalize(path))
            ? ValidationResult.Success
            : new ValidationResult("Ce fichier n'existe pas actuellement.");
    }

    public static ValidationResult? ValidateJavaMemory(string? text, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ValidationResult.Success;
        return int.TryParse(text, out var mb) && mb is >= 128 and <= 65536
            ? ValidationResult.Success
            : new ValidationResult("Indiquez une valeur en Mo entre 128 et 65536, ou laissez vide.");
    }
}
