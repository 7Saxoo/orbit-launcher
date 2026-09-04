using Orbit.Core.Models;

namespace Orbit.Core.Services;

/// <summary>Everything needed to register a new library entry from a picked file.</summary>
public sealed class NewAppRequest
{
    public required string ExecutablePath { get; init; }

    /// <summary>Optional override; when null a name is derived from the file.</summary>
    public string? Name { get; init; }

    public AppKind Kind { get; init; } = AppKind.Application;
    public string? Category { get; init; }
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? Description { get; init; }
    public bool IsFavorite { get; init; }

    // Populated by the automatic identification step (optional).
    public string? Publisher { get; init; }
    public string? Genre { get; init; }
    public string? CoverImagePath { get; init; }
}
