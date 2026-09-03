namespace Orbit.Core.Services;

public enum LaunchStatus
{
    /// <summary>The process was handed to the OS successfully.</summary>
    Started,

    /// <summary>The recorded executable path no longer exists.</summary>
    FileNotFound,

    /// <summary>The path exists but is not a launchable <c>.exe</c>.</summary>
    NotAnExecutable,

    /// <summary>Windows refused to start the process (permissions / policy).</summary>
    AccessDenied,

    /// <summary>The user dismissed the UAC elevation prompt.</summary>
    CancelledByUser,

    /// <summary>Any other failure; see <see cref="LaunchOutcome.Error"/>.</summary>
    Failed
}

/// <summary>Structured result of a launch attempt, safe to show to the user.</summary>
public sealed record LaunchOutcome(LaunchStatus Status, string Message, Exception? Error = null)
{
    public bool Succeeded => Status == LaunchStatus.Started;

    public static LaunchOutcome Ok(string exeName) =>
        new(LaunchStatus.Started, $"« {exeName} » a été lancé.");
}
