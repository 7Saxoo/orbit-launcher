using System.Windows.Threading;
using Orbit.App.ViewModels;
using Orbit.Core.Services;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Every few seconds, takes one process snapshot and updates the "En cours"
/// state of a set of tiles. One instance per grid view-model.
/// </summary>
public sealed class RunningStateTicker
{
    private readonly ILibraryService _library;
    private readonly DispatcherTimer _timer;
    private Func<IEnumerable<AppTileViewModel>>? _source;

    public RunningStateTicker(ILibraryService library)
    {
        _library = library;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Track(Func<IEnumerable<AppTileViewModel>> tiles)
    {
        _source = tiles;
        _timer.Start();
        Tick();
    }

    private async void Tick()
    {
        if (_source is null)
            return;

        var running = await Task.Run(_library.GetRunningImageNames).ConfigureAwait(true);
        foreach (var tile in _source())
            tile.UpdateRunningState(running);
    }
}
