using System.Collections.Concurrent;
using Orbit.Core.Data;
using Orbit.Core.Models;
using Orbit.Core.Services;

namespace Orbit.Tests.TestSupport;

/// <summary>In-memory <see cref="IAppRepository"/> for service-level tests.</summary>
public sealed class FakeAppRepository : IAppRepository
{
    private readonly ConcurrentDictionary<Guid, AppEntry> _store = new();

    public IReadOnlyList<AppEntry> Snapshot => _store.Values.ToList();
    public int DeleteAllCallCount { get; private set; }

    public Task<IReadOnlyList<AppEntry>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AppEntry>>(_store.Values.OrderBy(e => e.Name).ToList());

    public Task<AppEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var e) ? Clone(e) : null);

    public Task<bool> ExistsByPathAsync(string executablePath, CancellationToken ct = default)
    {
        var normalized = Orbit.Core.Infrastructure.PathHelper.Normalize(executablePath);
        var exists = _store.Values.Any(e =>
            string.Equals(Orbit.Core.Infrastructure.PathHelper.Normalize(e.ExecutablePath), normalized,
                StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(AppEntry entry, CancellationToken ct = default)
    {
        _store[entry.Id] = Clone(entry);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AppEntry entry, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(entry.Id))
            throw new InvalidOperationException("missing");
        _store[entry.Id] = Clone(entry);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task DeleteAllAsync(CancellationToken ct = default)
    {
        DeleteAllCallCount++;
        _store.Clear();
        return Task.CompletedTask;
    }

    public Task RecordLaunchAsync(Guid id, DateTimeOffset launchedAt, CancellationToken ct = default)
    {
        if (_store.TryGetValue(id, out var e))
        {
            e.LaunchCount++;
            e.LastLaunchedAt = launchedAt;
        }
        return Task.CompletedTask;
    }

    private static AppEntry Clone(AppEntry e) => e.Clone();
}

/// <summary>Configurable executable inspector – no file system involved.</summary>
public sealed class FakeExecutableInspector : IExecutableInspector
{
    public bool Exists { get; set; } = true;
    public bool HasExe { get; set; } = true;
    public string? SuggestedName { get; set; } = "Fake App";
    public string? FileDescription { get; set; } = "Fake description";
    public string? CompanyName { get; set; } = "Fake Corp";

    public ExecutableInfo Inspect(string path) => new()
    {
        NormalizedPath = Orbit.Core.Infrastructure.PathHelper.Normalize(path),
        Exists = Exists,
        HasExeExtension = HasExe,
        SuggestedName = SuggestedName,
        FileDescription = FileDescription,
        CompanyName = CompanyName
    };

    public AppAvailability Evaluate(string path) =>
        !HasExe ? AppAvailability.Invalid :
        !Exists ? AppAvailability.Missing :
        AppAvailability.Available;
}

/// <summary>Records the path it was asked about; returns a fixed result.</summary>
public sealed class FakeIconService : IIconService
{
    public string? Result { get; set; } = @"C:\cache\icon.png";
    public List<string> Requests { get; } = new();

    public Task<string?> EnsureIconAsync(string executablePath, CancellationToken ct = default)
    {
        Requests.Add(executablePath);
        return Task.FromResult(Result);
    }
}

/// <summary>Captures launch calls; returns a scripted outcome.</summary>
public sealed class FakeProcessLauncher : IProcessLauncher
{
    public LaunchOutcome NextOutcome { get; set; } = LaunchOutcome.Ok("fake.exe");
    public bool Running { get; set; }
    public List<AppEntry> Launched { get; } = new();

    public LaunchOutcome Launch(AppEntry entry)
    {
        Launched.Add(entry);
        return NextOutcome;
    }

    public bool IsRunning(AppEntry entry) => Running;
}
