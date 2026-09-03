using Orbit.Core.Data;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Serilog;

namespace Orbit.Core.Detection;

/// <inheritdoc />
public sealed class AppDetectionService : IAppDetectionService
{
    private readonly IReadOnlyList<IInstalledAppSource> _sources;
    private readonly IAppRepository _repository;
    private readonly IExecutableInspector _inspector;
    private readonly ILogger _log;

    public AppDetectionService(
        IEnumerable<IInstalledAppSource> sources,
        IAppRepository repository,
        IExecutableInspector inspector,
        ILogger log)
    {
        _sources = sources.ToList();
        _repository = repository;
        _inspector = inspector;
        _log = log.ForContext<AppDetectionService>();
    }

    public async Task<DetectionResult> ScanAsync(CancellationToken ct = default)
    {
        var raw = await Task.Run(() => Collect(ct), ct).ConfigureAwait(false);

        var existing = (await _repository.GetAllAsync(ct).ConfigureAwait(false))
            .Select(e => PathHelper.Normalize(e.ExecutablePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var kept = new List<DetectedApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var alreadyInLibrary = 0;
        var totalFound = 0;

        foreach (var app in raw)
        {
            var path = PathHelper.Normalize(app.ExecutablePath);
            if (path.Length == 0 || !PathHelper.HasExecutableExtension(path))
                continue;

            totalFound++;

            if (existing.Contains(path))
            {
                alreadyInLibrary++;
                continue;
            }

            if (!seen.Add(path))
                continue;

            if (_inspector.Evaluate(path) != AppAvailability.Available)
                continue;

            kept.Add(app with { ExecutablePath = path });
        }

        kept.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        _log.Information("Detection: {Found} found, {New} new, {Known} already in library",
            totalFound, kept.Count, alreadyInLibrary);

        return new DetectionResult(kept, alreadyInLibrary, totalFound);
    }

    private IReadOnlyList<DetectedApp> Collect(CancellationToken ct)
    {
        var results = new List<DetectedApp>();
        foreach (var source in _sources)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var found = source.Scan(ct).ToList();
                _log.Debug("Source {Source} returned {Count} candidates", source.DisplayName, found.Count);
                results.AddRange(found);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Detection source {Source} failed", source.DisplayName);
            }
        }

        return results;
    }
}
