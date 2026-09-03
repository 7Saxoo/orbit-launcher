namespace Orbit.Core.Models;

/// <summary>
/// Runtime status of the executable backing a library entry. This is always
/// recomputed from the file system and is never persisted, so an entry whose
/// file is temporarily on a disconnected drive is never lost.
/// </summary>
public enum AppAvailability
{
    /// <summary>The executable exists and looks launchable.</summary>
    Available = 0,

    /// <summary>The recorded path no longer points to an existing file.</summary>
    Missing = 1,

    /// <summary>The path exists but is not a regular file we can launch.</summary>
    Invalid = 2
}
