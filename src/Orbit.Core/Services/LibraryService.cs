using Orbit.Core.Data;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Serilog;

namespace Orbit.Core.Services;

/// <inheritdoc />
public sealed class LibraryService : ILibraryService
{
    private readonly IAppRepository _repository;
    private readonly IExecutableInspector _inspector;
    private readonly IIconService _icons;
    private readonly IProcessLauncher _launcher;
    private readonly ILogger _log;

    public LibraryService(
        IAppRepository repository,
        IExecutableInspector inspector,
        IIconService icons,
        IProcessLauncher launcher,
        ILogger log)
    {
        _repository = repository;
        _inspector = inspector;
        _icons = icons;
        _launcher = launcher;
        _log = log.ForContext<LibraryService>();
    }

    public async Task<IReadOnlyList<LibraryItem>> LoadAsync(CancellationToken ct = default)
    {
        var entries = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        var items = new List<LibraryItem>(entries.Count);
        foreach (var entry in entries)
            items.Add(new LibraryItem(entry, _inspector.Evaluate(entry.ExecutablePath)));

        _log.Debug("Loaded {Count} library entries", items.Count);
        return items;
    }

    public async Task<AppEntry> AddAsync(NewAppRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var info = _inspector.Inspect(request.ExecutablePath);
        if (!info.Exists)
            throw new ExecutableNotRegisterableException(
                $"Le fichier sélectionné est introuvable :\n{info.NormalizedPath}");
        if (!info.HasExeExtension)
            throw new ExecutableNotRegisterableException(
                $"Seuls les fichiers .exe peuvent être ajoutés :\n{info.NormalizedPath}");

        if (await _repository.ExistsByPathAsync(info.NormalizedPath, ct).ConfigureAwait(false))
            throw new DuplicateAppException(info.NormalizedPath);

        var name = Coalesce(request.Name, info.SuggestedName)
                   ?? Path.GetFileNameWithoutExtension(info.NormalizedPath);

        var iconPath = await _icons.EnsureIconAsync(info.NormalizedPath, ct).ConfigureAwait(false);

        var entry = new AppEntry
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ExecutablePath = info.NormalizedPath,
            Arguments = Coalesce(request.Arguments, null),
            WorkingDirectory = Coalesce(request.WorkingDirectory, null),
            Kind = request.Kind,
            Category = request.Category?.Trim() ?? string.Empty,
            Description = Coalesce(request.Description, info.FileDescription),
            IconCachePath = iconPath,
            DateAdded = DateTimeOffset.Now,
            IsFavorite = request.IsFavorite
        };

        await _repository.AddAsync(entry, ct).ConfigureAwait(false);
        _log.Information("Added «{Name}» ({Kind}) from {Path}", entry.Name, entry.Kind, entry.ExecutablePath);
        return entry;
    }

    public async Task<AppEntry> UpdateAsync(AppEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var existing = await _repository.GetByIdAsync(entry.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entry {entry.Id:D} no longer exists.");

        entry.ExecutablePath = PathHelper.Normalize(entry.ExecutablePath);
        entry.Name = string.IsNullOrWhiteSpace(entry.Name)
            ? existing.Name
            : entry.Name.Trim();

        // Preserve statistics that the edit form never shows.
        entry.DateAdded = existing.DateAdded;
        entry.LaunchCount = existing.LaunchCount;
        entry.LastLaunchedAt = existing.LastLaunchedAt;

        if (!PathHelper.AreSamePath(existing.ExecutablePath, entry.ExecutablePath))
        {
            _log.Information("Executable path changed for {Id}; refreshing icon", entry.Id);
            entry.IconCachePath = await _icons.EnsureIconAsync(entry.ExecutablePath, ct).ConfigureAwait(false);
        }

        await _repository.UpdateAsync(entry, ct).ConfigureAwait(false);
        return entry;
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct).ConfigureAwait(false);
        _log.Information("Removed entry {Id} (executable left untouched on disk)", id);
    }

    public async Task<AppEntry> SetFavoriteAsync(Guid id, bool favorite, CancellationToken ct = default)
    {
        var entry = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entry {id:D} no longer exists.");

        entry.IsFavorite = favorite;
        await _repository.UpdateAsync(entry, ct).ConfigureAwait(false);
        return entry;
    }

    public async Task<LaunchOutcome> LaunchAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entry {id:D} no longer exists.");

        var outcome = _launcher.Launch(entry);
        if (outcome.Succeeded)
            await _repository.RecordLaunchAsync(id, DateTimeOffset.Now, ct).ConfigureAwait(false);

        return outcome;
    }

    public AppAvailability Evaluate(AppEntry entry) =>
        _inspector.Evaluate(entry.ExecutablePath);

    private static string? Coalesce(string? preferred, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred)) return preferred.Trim();
        if (!string.IsNullOrWhiteSpace(fallback)) return fallback.Trim();
        return null;
    }
}
