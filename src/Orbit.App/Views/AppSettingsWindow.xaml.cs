using System.Windows;
using Orbit.App.Infrastructure;
using Orbit.App.ViewModels;

namespace Orbit.App.Views;

public partial class AppSettingsWindow : Window
{
    public AppSettingsWindow()
    {
        InitializeComponent();
        WindowThemeHelper.Attach(this);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is AppSettingsViewModel oldVm)
                oldVm.RequestClose -= OnRequestClose;
            if (e.NewValue is AppSettingsViewModel newVm)
                newVm.RequestClose += OnRequestClose;
        };
    }

    private void OnRequestClose(object? sender, EventArgs e) => Close();
}
