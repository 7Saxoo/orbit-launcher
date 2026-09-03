using Orbit.Core.Models;

namespace Orbit.Core.Data;

/// <summary>Persistence boundary for <see cref="AppEntry"/> records.</summary>
public interface IAppRepository
{
    Task<IReadOnlyList<AppEntry>> GetAllAsync(CancellationToken ct = default);

    Task<AppEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>True when an entry already points at the same executable path.</summary>
    Task<bool> ExistsByPathAsync(string executablePath, CancellationToken ct = default);

    Task AddAsync(AppEntry entry, CancellationToken ct = default);

    Task UpdateAsync(AppEntry entry, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Removes every entry. Used by the "reset data" action.</summary>
    Task DeleteAllAsync(CancellationToken ct = default);

    /// <summary>Atomically bumps the launch counter and last-launched timestamp.</summary>
    Task RecordLaunchAsync(Guid id, DateTimeOffset launchedAt, CancellationToken ct = default);
}
