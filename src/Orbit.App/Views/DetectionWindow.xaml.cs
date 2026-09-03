using System.Windows;
using Orbit.App.ViewModels;

namespace Orbit.App.Views;

/// <summary>Modal auto-detection dialog. The view-model does the scanning and
/// importing; this only wires the two buttons.</summary>
public partial class DetectionWindow : Window
{
    public DetectionWindow()
    {
        InitializeComponent();
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DetectionViewModel vm)
            return;

        ImportButton.IsEnabled = false;
        var added = await vm.ImportSelectedAsync();

        // Close once something was imported; otherwise let the user adjust.
        if (added > 0)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ImportButton.IsEnabled = true;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
