using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Puts Orbit in the notification area. Closing the main window hides it there
/// (and drops power use) instead of quitting, unless the user turned that off or
/// picked "Quitter" from the tray menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ILogger _log;

    private TaskbarIcon? _tray;
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

        _tray = new TaskbarIcon
        {
            ToolTipText = "Orbit",
            IconSource = new BitmapImage(
                new Uri("pack://application:,,,/Orbit;component/Assets/orbit.ico", UriKind.Absolute)),
            ContextMenu = BuildMenu()
        };
        _tray.TrayMouseDoubleClick += (_, _) => Restore();

        // Only *closing* the window sends Orbit to the tray. Minimising with the
        // caption button behaves like a normal window minimise.
        window.Closing += OnClosing;
        Application.Current.Exit += (_, _) => Dispose();
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Ouvrir Orbit" };
        open.Click += (_, _) => Restore();

        var quit = new MenuItem { Header = "Quitter" };
        quit.Click += (_, _) => QuitReally();

        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);
        return menu;
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
        try
        {
            _tray?.ShowBalloonTip("Orbit",
                "Toujours là, en arrière-plan — double-cliquez l'icône pour revenir.", BalloonIcon.Info);
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Balloon tip failed");
        }

        _log.Information("Orbit hidden to tray, low-power mode engaged");
    }

    private void Restore()
    {
        if (_window is null)
            return;

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
        _tray?.Dispose();
        _tray = null;
    }
}
