using System.Windows;
using Orbit.App.Services;
using Orbit.Core.Services;

namespace Orbit.App;

/// <summary>Interaction logic for MainWindow.xaml. Behaviour lives in
/// <see cref="ViewModels.MainViewModel"/>; this applies and persists the window
/// size and implements <see cref="IWindowService"/>.</summary>
public partial class MainWindow : Window, IWindowService
{
    private readonly ISettingsService _settings;

    public MainWindow(ISettingsService settings)
    {
        _settings = settings;
        InitializeComponent();

        var s = settings.Current;
        Width = Math.Max(MinWidth, s.WindowWidth);
        Height = Math.Max(MinHeight, s.WindowHeight);
        WindowState = s.WindowMaximized ? WindowState.Maximized : WindowState.Normal;

        Application.Current.Exit += (_, _) => PersistSize();
    }

    public void ApplySize(int width, int height, bool maximized)
    {
        if (maximized)
        {
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            Width = width;
            Height = height;
        }
    }

    private void PersistSize()
    {
        try
        {
            var s = _settings.Current.Clone();
            s.WindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                s.WindowWidth = (int)ActualWidth;
                s.WindowHeight = (int)ActualHeight;
            }
            _ = _settings.SaveAsync(s);
        }
        catch
        {
            // best effort on shutdown
        }
    }
}
