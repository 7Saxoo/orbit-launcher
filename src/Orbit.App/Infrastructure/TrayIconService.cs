using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Puts Orbit in the notification area. Closing the main window hides it there
/// (and drops power use) instead of quitting, unless the user turned that off in
/// Settings or picked "Quitter" from the tray menu. The menu opens at the mouse,
/// next to the icon.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ILogger _log;

    private TaskbarIcon? _tray;
    private ContextMenu? _menu;
    private Window? _window;
    private bool _exiting;

    public TrayIconService(ISettingsService settings, ILogger log)
    {
        _settings = settings;
        _log = log.ForContext<TrayIconService>();
    }

    public void Attach(Window window)
    {
        _window = window;
        _menu = BuildMenu();

        _tray = new TaskbarIcon
        {
            ToolTipText = "Orbit",
            IconSource = new BitmapImage(
                new Uri("pack://application:,,,/Orbit;component/Assets/orbit.ico", UriKind.Absolute))
        };
        _tray.TrayMouseDoubleClick += (_, _) => Restore();
        _tray.TrayLeftMouseUp += (_, _) => Restore();
        _tray.TrayRightMouseUp += (_, _) => OpenMenuAtMouse();

        window.Closing += OnClosing;
        Application.Current.Exit += (_, _) => Dispose();
    }

    private ContextMenu BuildMenu()
    {
        var open = new MenuItem { Header = "Ouvrir Orbit" };
        open.Click += (_, _) => Restore();

        var quit = new MenuItem { Header = "Quitter" };
        quit.Click += (_, _) => QuitReally();

        var menu = new ContextMenu { Placement = PlacementMode.MousePoint, StaysOpen = false };
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);
        return menu;
    }

    private void OpenMenuAtMouse()
    {
        if (_menu is null)
            return;

        // Own the popup ourselves so it appears at the cursor (next to the icon)
        // instead of the screen corner, even while the main window is hidden.
        _menu.Placement = PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting || !_settings.Current.MinimizeToTrayOnClose)
            return;

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        if (_window is null)
            return;

        _window.Hide();
        PowerManager.EnterLowPower();
        _log.Information("Orbit hidden to tray, low-power mode engaged");
    }

    private void Restore()
    {
        if (_window is null)
            return;

        if (_menu is not null)
            _menu.IsOpen = false;

        PowerManager.ExitLowPower();
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        _log.Information("Orbit restored from tray");
    }

    private void QuitReally()
    {
        _exiting = true;
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_menu is not null)
            _menu.IsOpen = false;
        _tray?.Dispose();
        _tray = null;
    }
}
