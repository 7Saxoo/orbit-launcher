using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orbit.App.Infrastructure;
using Orbit.App.Services;
using Orbit.App.ViewModels;
using Orbit.Core;
using Orbit.Core.Data;
using Orbit.Core.Infrastructure;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App;

/// <summary>Application entry point: builds the DI container, initialises the
/// database, applies the theme and shows the shell. Also the last line of
/// defence for unhandled exceptions.</summary>
public partial class App : Application
{
    private IHost? _host;
    private ILogger _log = Serilog.Core.Logger.None;
    private SingleInstanceGuard? _instanceGuard;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance: a second launch just wakes the running one.
        _instanceGuard = new SingleInstanceGuard();
        if (!_instanceGuard.TryAcquire())
        {
            SingleInstanceGuard.SignalPrimary();
            _instanceGuard.Dispose();
            _instanceGuard = null;
            Shutdown();
            return;
        }

        var paths = new OrbitPaths();
        paths.EnsureDirectories();

        Log.Logger = OrbitLogging.Create(paths);
        _log = Log.Logger.ForContext<App>();
        _log.Information("Orbit starting (v{Version})",
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _host = BuildHost(paths);
            _host.Services.GetRequiredService<DatabaseInitializer>().Initialize();

            var settings = _host.Services.GetRequiredService<ISettingsService>();
            settings.Load();
            _host.Services.GetRequiredService<ThemeManager>()
                .Apply(settings.Current.Theme, settings.Current.Temperature);

            var main = _host.Services.GetRequiredService<MainViewModel>();
            var window = _host.Services.GetRequiredService<MainWindow>();
            window.DataContext = main;
            MainWindow = window;
            _host.Services.GetRequiredService<TrayIconService>().Attach(window);
            window.Show();

            if (_instanceGuard is not null)
                _instanceGuard.ActivationRequested += () => Dispatcher.Invoke(BringMainWindowToFront);

            await main.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Fatal(ex, "Fatal error during start-up");
            MessageBox.Show(
                $"Orbit n'a pas pu démarrer :\n\n{ex.Message}\n\nConsultez les journaux dans :\n{paths.LogDirectory}",
                "Erreur de démarrage", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log.Information("Orbit exiting (code {Code})", e.ApplicationExitCode);
        _instanceGuard?.Dispose();
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void BringMainWindowToFront()
    {
        if (MainWindow is not { } window)
            return;

        PowerManager.ExitLowPower();
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        _log.Information("Activated by a second launch");
    }

    private static IHost BuildHost(OrbitPaths paths) =>
        Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILogger>(_ => Log.Logger);
                services.AddOrbitCore(paths);

                // Online game identification (inert until IGDB keys are set in Settings).
                services.AddSingleton(_ => new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(25)
                });
                services.AddSingleton<Orbit.Core.Identification.IIdentificationProvider,
                    Orbit.Core.Identification.IgdbGameProvider>();

                services.AddSingleton<ThemeManager>();
                services.AddSingleton<TrayIconService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<AppTileContext>();
                services.AddSingleton<AddAppFlow>();

                services.AddTransient<DetectionViewModel>();
                services.AddSingleton<Func<DetectionViewModel>>(sp => sp.GetRequiredService<DetectionViewModel>);
                services.AddSingleton<DetectionFlow>();

                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<LibraryViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainViewModel>();

                services.AddSingleton<MainWindow>();
                services.AddSingleton<IWindowService>(sp => sp.GetRequiredService<MainWindow>());
            })
            .Build();

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log.Error(e.Exception, "Unhandled UI exception");
        MessageBox.Show(
            $"Une erreur inattendue est survenue :\n\n{e.Exception.Message}",
            "Orbit", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            _log.Fatal(ex, "Unhandled domain exception (terminating: {Terminating})", e.IsTerminating);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
