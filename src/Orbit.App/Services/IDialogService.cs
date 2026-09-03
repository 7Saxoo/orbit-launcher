using Orbit.App.ViewModels;

namespace Orbit.App.Services;

/// <summary>All modal/OS interaction the view-models need, behind an interface
/// so they stay testable and free of <c>System.Windows</c> dialog types.</summary>
public interface IDialogService
{
    /// <summary>Shows an "open file" dialog filtered to <c>.exe</c>. Returns the
    /// chosen path, or null if cancelled.</summary>
    string? PickExecutable(string? initialDirectory = null);

    /// <summary>Shows a folder picker. Returns the chosen path, or null.</summary>
    string? PickFolder(string? initialDirectory = null);

    /// <summary>Yes/No confirmation. Returns true when the user confirms.</summary>
    bool Confirm(string title, string message);

    void ShowError(string title, string message);

    void ShowInfo(string title, string message);

    /// <summary>Shows the add/edit form modally. Returns true if the user saved.</summary>
    bool ShowAppForm(AppFormViewModel viewModel);

    /// <summary>Shows the auto-detection dialog modally.</summary>
    void ShowDetection(DetectionViewModel viewModel);

    /// <summary>Opens Explorer with the given file selected (or the folder).</summary>
    void RevealInExplorer(string path);
}
