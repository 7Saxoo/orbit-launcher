using Orbit.Core.Models;

namespace Orbit.Core.Services;

/// <summary>Starts the executables backing library entries.</summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Attempts to start the entry's executable. Never throws for expected
    /// failure modes – every outcome is reported through <see cref="LaunchOutcome"/>.
    /// </summary>
    LaunchOutcome Launch(AppEntry entry);

    /// <summary>
    /// Best-effort check for an already-running process with the same image
    /// name. Used only to warn the user; it never blocks a launch.
    /// </summary>
    bool IsRunning(AppEntry entry);

    /// <summary>
    /// Lower-cased image names (without extension) of every currently running
    /// process – one enumeration to test many entries against.
    /// </summary>
    IReadOnlySet<string> GetRunningImageNames();
}
