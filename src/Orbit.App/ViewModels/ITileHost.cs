namespace Orbit.App.ViewModels;

public enum StatusSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Callback surface an <see cref="AppTileViewModel"/> uses to talk back to the
/// shell: request a full data refresh after a mutation, and post a transient
/// status-bar message. Implemented by <see cref="MainViewModel"/>.
/// </summary>
public interface ITileHost
{
    Task RefreshAllAsync();

    void SetStatus(string message, StatusSeverity severity = StatusSeverity.Info);
}
