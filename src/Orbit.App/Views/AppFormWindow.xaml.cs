using System.Windows;
using Orbit.App.Infrastructure;
using Orbit.App.ViewModels;

namespace Orbit.App.Views;

/// <summary>Modal add/edit form. Validation lives in <see cref="AppFormViewModel"/>;
/// this only gates the dialog result.</summary>
public partial class AppFormWindow : Window
{
    public AppFormWindow()
    {
        InitializeComponent();
        WindowThemeHelper.Attach(this);
        UiScaleManager.Track(this);

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is AppFormViewModel oldVm)
                oldVm.CloseRequested -= OnCloseRequested;
            if (e.NewValue is AppFormViewModel newVm)
                newVm.CloseRequested += OnCloseRequested;
        };
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppFormViewModel vm)
            return;

        if (!vm.Validate())
        {
            FormError.Visibility = Visibility.Visible;
            return;
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
