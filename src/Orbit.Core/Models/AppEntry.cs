namespace Orbit.Core.Models;

/// <summary>
/// A single application or game registered in the user's library.
/// This is a plain data record persisted by <c>IAppRepository</c>; it carries
/// no file-system state (see <see cref="AppAvailability"/> for that).
/// </summary>
public sealed class AppEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name. Never empty once persisted.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute path to the <c>.exe</c> to launch.</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>Optional command-line arguments passed on launch.</summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// Optional working directory. When null the directory containing the
    /// executable is used.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    public AppKind Kind { get; set; } = AppKind.Application;

    /// <summary>Free-text category ("Bureautique", "RPG", ...). May be empty.</summary>
    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Absolute path to the cached PNG icon, or null when no icon could be
    /// extracted and the default should be shown.
    /// </summary>
    public string? IconCachePath { get; set; }

    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.Now;

    public int LaunchCount { get; set; }

    public DateTimeOffset? LastLaunchedAt { get; set; }

    public bool IsFavorite { get; set; }

    // ---- Reserved for a later "rich game metadata" iteration -----------------
    // Persisted (nullable columns) so the schema is forward-compatible, but not
    // surfaced in the V1 UI. See README "Prochaines évolutions".
    public string? Genre { get; set; }
    public string? Platform { get; set; }
    public string? Publisher { get; set; }
    public string? CoverImagePath { get; set; }
    public long? PlayTimeSeconds { get; set; }

    /// <summary>Returns a deep copy, used by edit dialogs to avoid mutating the live entry.</summary>
    public AppEntry Clone() => (AppEntry)MemberwiseClone();
}
