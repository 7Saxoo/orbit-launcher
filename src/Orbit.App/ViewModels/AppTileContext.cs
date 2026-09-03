using Orbit.App.Services;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.App.ViewModels;

/// <summary>
/// Bundle of collaborators every <see cref="AppTileViewModel"/> needs, so tile
/// construction stays a two-argument call. Registered as a singleton;
/// <see cref="Host"/> is wired by <see cref="MainViewModel"/> once the shell
/// exists (it cannot be a constructor dependency without a DI cycle).
/// </summary>
public sealed class AppTileContext
{
    public AppTileContext(
        ILibraryService library,
        IExecutableInspector inspector,
        IDialogService dialogs,
        ISettingsService settings,
        ILogger log)
    {
        Library = library;
        Inspector = inspector;
        Dialogs = dialogs;
        Settings = settings;
        Log = log;
    }

    public ILibraryService Library { get; }
    public IExecutableInspector Inspector { get; }
    public IDialogService Dialogs { get; }
    public ISettingsService Settings { get; }
    public ILogger Log { get; }

    /// <summary>Set exactly once by <see cref="MainViewModel"/>.</summary>
    public ITileHost Host { get; set; } = NullTileHost.Instance;

    private sealed class NullTileHost : ITileHost
    {
        public static readonly NullTileHost Instance = new();
        public Task RefreshAllAsync() => Task.CompletedTask;
        public void SetStatus(string message, StatusSeverity severity = StatusSeverity.Info) { }
    }
}
