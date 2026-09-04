using Orbit.Core.Detection;
using Orbit.Core.Models;

namespace Orbit.Core.Services;

/// <summary>
/// Application-facing orchestration over the repository, executable inspector,
/// icon cache and process launcher. View-models talk only to this.
/// </summary>
public interface ILibraryService
{
    /// <summary>Loads every entry with its current file-system availability.</summary>
    Task<IReadOnlyList<LibraryItem>> LoadAsync(CancellationToken ct = default);

    /// <summary>Registers a new entry from a picked executable.</summary>
    /// <exception cref="DuplicateAppException" />
    /// <exception cref="ExecutableNotRegisterableException" />
    Task<AppEntry> AddAsync(NewAppRequest request, CancellationToken ct = default);

    /// <summary>Bulk-imports detected apps, skipping any that fail to register or
    /// already exist. Returns the number actually added.</summary>
    Task<int> ImportAsync(IEnumerable<DetectedApp> apps, CancellationToken ct = default);

    /// <summary>Persists edits to an existing entry, refreshing its icon if the
    /// executable path changed.</summary>
    Task<AppEntry> UpdateAsync(AppEntry entry, CancellationToken ct = default);

    /// <summary>Removes an entry. The executable on disk is never touched.</summary>
    Task RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Removes several entries in one go. Executables are never touched.
    /// Returns the number actually removed.</summary>
    Task<int> RemoveManyAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<AppEntry> SetFavoriteAsync(Guid id, bool favorite, CancellationToken ct = default);

    /// <summary>Launches an entry and records the launch in its statistics.</summary>
    Task<LaunchOutcome> LaunchAsync(Guid id, CancellationToken ct = default);

    /// <summary>Re-evaluates a single entry's availability against the file system.</summary>
    AppAvailability Evaluate(AppEntry entry);

    /// <summary>Best-effort check for an already-running instance of the entry's executable.</summary>
    bool IsRunning(AppEntry entry);

    /// <summary>Lower-cased image names of every running process – one snapshot to
    /// test many tiles against.</summary>
    IReadOnlySet<string> GetRunningImageNames();

    /// <summary>Removes every entry and clears the icon cache. Irreversible –
    /// callers must confirm with the user first.</summary>
    Task ResetAsync(CancellationToken ct = default);

    /// <summary>Re-extracts the icon of every entry (folder fallback + large sizes).
    /// Returns how many entries got a different icon.</summary>
    Task<int> RefreshIconsAsync(CancellationToken ct = default);
}
