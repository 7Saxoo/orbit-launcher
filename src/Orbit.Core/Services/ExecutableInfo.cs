using Orbit.Core.Models;

namespace Orbit.Core.Services;

/// <summary>Result of inspecting a candidate executable on disk.</summary>
public sealed record ExecutableInfo
{
    public required string NormalizedPath { get; init; }
    public required bool Exists { get; init; }
    public required bool HasExeExtension { get; init; }

    /// <summary>Best guess at a display name, derived from version metadata or the file name.</summary>
    public string? SuggestedName { get; init; }

    /// <summary>Product / company metadata when the PE file carries a version block.</summary>
    public string? ProductName { get; init; }
    public string? CompanyName { get; init; }
    public string? FileDescription { get; init; }
    public string? FileVersion { get; init; }

    public AppAvailability Availability =>
        !Exists ? AppAvailability.Missing :
        !HasExeExtension ? AppAvailability.Invalid :
        AppAvailability.Available;

    /// <summary>True when the file can be registered as a library entry.</summary>
    public bool IsRegisterable => Exists && HasExeExtension;
}
