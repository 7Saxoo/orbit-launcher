using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Orbit.App.ViewModels;
using Orbit.App.Views;
using Orbit.Core.Infrastructure;
using Serilog;

namespace Orbit.App.Services;

/// <inheritdoc />
public sealed class DialogService : IDialogService
{
    private readonly ILogger _log;

    public DialogService(ILogger log) => _log = log.ForContext<DialogService>();

    public string? PickExecutable(string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Sélectionner un fichier exécutable",
            Filter = "Programmes (*.exe)|*.exe|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Sélectionner un dossier de travail"
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;

    public void ShowError(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool ShowAppForm(AppFormViewModel viewModel)
    {
        var window = new AppFormWindow { DataContext = viewModel };
        var owner = Owner;
        if (owner is not null && !ReferenceEquals(owner, window))
            window.Owner = owner;

        return window.ShowDialog() == true;
    }

    public void ShowDetection(DetectionViewModel viewModel)
    {
        var window = new DetectionWindow { DataContext = viewModel };
        var owner = Owner;
        if (owner is not null && !ReferenceEquals(owner, window))
            window.Owner = owner;

        window.ShowDialog();
    }

    public void RevealInExplorer(string path)
    {
        try
        {
            var normalized = PathHelper.Normalize(path);

            if (File.Exists(normalized))
            {
                Start("explorer.exe", $"/select,\"{normalized}\"");
                return;
            }

            var directory = Directory.Exists(normalized)
                ? normalized
                : PathHelper.GetContainingDirectory(normalized);

            if (directory is not null && Directory.Exists(directory))
                Start("explorer.exe", $"\"{directory}\"");
            else
                ShowInfo("Emplacement introuvable",
                    $"Le dossier n'existe plus :\n{normalized}");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not reveal {Path} in Explorer", path);
            ShowError("Action impossible", $"Impossible d'ouvrir l'explorateur :\n{ex.Message}");
        }
    }

    private static void Start(string fileName, string arguments) =>
        Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true });

    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;
}
