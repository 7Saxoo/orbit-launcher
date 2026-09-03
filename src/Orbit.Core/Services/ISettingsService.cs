namespace Orbit.Core.Services;

/// <summary>Loads and persists <see cref="AppSettings"/>.</summary>
public interface ISettingsService
{
    /// <summary>The currently loaded settings. Never null after <see cref="Load"/>.</summary>
    AppSettings Current { get; }

    /// <summary>Raised after <see cref="Save"/> succeeds.</summary>
    event EventHandler? Changed;

    /// <summary>Reads settings from disk, creating defaults on first run and
    /// recovering gracefully from a corrupt file.</summary>
    void Load();

    /// <summary>Persists the given settings and updates <see cref="Current"/>.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
