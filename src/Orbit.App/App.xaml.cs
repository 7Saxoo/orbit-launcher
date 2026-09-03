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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
            _host.Services.GetRequiredService<ISettingsService>().Load();
            _host.Services.GetRequiredService<ThemeManager>()
                .Apply(_host.Services.GetRequiredService<ISettingsService>().Current.Theme);

            var main = _host.Services.GetRequiredService<MainViewModel>();
            var window = _host.Services.GetRequiredService<MainWindow>();
            window.DataContext = main;
            MainWindow = window;
            window.Show();

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
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static IHost BuildHost(OrbitPaths paths) =>
        Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILogger>(_ => Log.Logger);
                services.AddOrbitCore(paths);

                services.AddSingleton<ThemeManager>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<AppTileContext>();
                services.AddSingleton<AddAppFlow>();

                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<LibraryViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainViewModel>();

                services.AddSingleton<MainWindow>();
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
