using System.Windows;
using Orbit.App.Infrastructure;
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
        WindowThemeHelper.Attach(this);
        UiScaleManager.Track(this);

        var s = settings.Current;
        if (s.WindowWidth <= 0 || s.WindowHeight <= 0)
            FitToScreen();
        else if (s.WindowMaximized)
            WindowState = WindowState.Maximized;
        else
        {
            Width = Math.Max(MinWidth, s.WindowWidth);
            Height = Math.Max(MinHeight, s.WindowHeight);
        }

        Application.Current.Exit += (_, _) => PersistSize();
    }

    public void ApplySize(int width, int height, bool maximized)
    {
        if (maximized)
        {
            WindowState = WindowState.Maximized;
        }
        else if (width <= 0 || height <= 0)
        {
            WindowState = WindowState.Normal;
            FitToScreen();
        }
        else
        {
            WindowState = WindowState.Normal;
            Width = width;
            Height = height;
            RecentreOnScreen();
        }
    }

    /// <summary>Sizes the window to ~92% of the current screen's work area and centres it.</summary>
    private void FitToScreen()
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, area.Width * 0.92);
        Height = Math.Max(MinHeight, area.Height * 0.92);
        RecentreOnScreen();
    }

    private void RecentreOnScreen()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - Height) / 2;
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
