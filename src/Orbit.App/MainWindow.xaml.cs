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

    /// <summary>
    /// True while the window follows "Adaptée à l'écran": it fills the work area
    /// on every launch and its exact size is never persisted (so it keeps
    /// re-fitting even after a manual resize during the session).
    /// </summary>
    private bool _autoFit;

    public MainWindow(ISettingsService settings)
    {
        _settings = settings;
        InitializeComponent();
        WindowThemeHelper.Attach(this);
        UiScaleManager.Track(this);

        var s = settings.Current;
        if (s.WindowMaximized)
        {
            _autoFit = false;
            WindowState = WindowState.Maximized;
        }
        else if (s.WindowWidth <= 0 || s.WindowHeight <= 0)
        {
            _autoFit = true;
            FillWorkArea();
        }
        else
        {
            _autoFit = false;
            Width = Math.Max(MinWidth, s.WindowWidth);
            Height = Math.Max(MinHeight, s.WindowHeight);
            RecentreOnScreen();
        }

        Application.Current.Exit += (_, _) => PersistSize();
    }

    public void ApplySize(int width, int height, bool maximized)
    {
        if (maximized)
        {
            _autoFit = false;
            WindowState = WindowState.Maximized;
        }
        else if (width <= 0 || height <= 0)
        {
            _autoFit = true;
            WindowState = WindowState.Normal;
            FillWorkArea();
        }
        else
        {
            _autoFit = false;
            WindowState = WindowState.Normal;
            Width = width;
            Height = height;
            RecentreOnScreen();
        }

        PersistSize();
    }

    /// <summary>Sizes the window to the current screen's usable area (minus the taskbar).</summary>
    private void FillWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left;
        Top = area.Top;
        Width = Math.Max(MinWidth, area.Width);
        Height = Math.Max(MinHeight, area.Height);
    }

    private void RecentreOnScreen()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
    }

    private void PersistSize()
    {
        try
        {
            var s = _settings.Current.Clone();

            if (_autoFit)
            {
                // Keep the "fit to screen" sentinel so it re-fits next launch.
                s.WindowWidth = 0;
                s.WindowHeight = 0;
                s.WindowMaximized = false;
            }
            else
            {
                s.WindowMaximized = WindowState == WindowState.Maximized;
                if (WindowState == WindowState.Normal)
                {
                    s.WindowWidth = (int)ActualWidth;
                    s.WindowHeight = (int)ActualHeight;
                }
            }

            _ = _settings.SaveAsync(s);
        }
        catch
        {
            // best effort on shutdown
        }
    }
}
