using System.Windows;
using System.Windows.Media;
using Orbit.App.Infrastructure;

namespace Orbit.App.Views;

/// <summary>A small modal that matches the current Orbit palette – used for
/// confirmations and error/info messages instead of the OS <c>MessageBox</c>.</summary>
public partial class AppDialogWindow : Window
{
    public enum DialogKind { Question, Error, Info }

    private bool _result;

    private AppDialogWindow()
    {
        InitializeComponent();
        WindowThemeHelper.Attach(this);
        UiScaleManager.Track(this);
    }

    public static bool Show(Window? owner, string title, string message, DialogKind kind, bool confirm)
    {
        var dlg = new AppDialogWindow();
        if (owner is not null && !ReferenceEquals(owner, dlg))
            dlg.Owner = owner;

        dlg.TitleText.Text = title;
        dlg.MessageText.Text = message;

        switch (kind)
        {
            case DialogKind.Error:
                dlg.Glyph.Text = "!";
                dlg.Glyph.SetResourceReference(ForegroundProperty, "Brush.Danger");
                break;
            case DialogKind.Info:
                dlg.Glyph.Text = "i";
                break;
            default:
                dlg.Glyph.Text = "?";
                break;
        }

        if (confirm)
        {
            dlg.PrimaryButton.Content = "Oui";
            dlg.SecondaryButton.Content = "Non";
        }
        else
        {
            dlg.PrimaryButton.Content = "OK";
            dlg.SecondaryButton.Visibility = Visibility.Collapsed;
        }

        dlg.ShowDialog();
        return dlg._result;
    }

    private void OnPrimary(object sender, RoutedEventArgs e)
    {
        _result = true;
        Close();
    }

    private void OnSecondary(object sender, RoutedEventArgs e)
    {
        _result = false;
        Close();
    }
}
