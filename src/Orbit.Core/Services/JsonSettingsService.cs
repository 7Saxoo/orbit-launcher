using System.Text.Json;
using System.Text.Json.Serialization;
using Orbit.Core.Infrastructure;
using Serilog;

namespace Orbit.Core.Services;

/// <summary>JSON-file implementation of <see cref="ISettingsService"/>. A
/// corrupt file is moved aside (never deleted) and defaults are restored.</summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly OrbitPaths _paths;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private AppSettings _current = new();

    public JsonSettingsService(OrbitPaths paths, ILogger log)
    {
        _paths = paths;
        _log = log.ForContext<JsonSettingsService>();
    }

    public AppSettings Current => _current;

    public event EventHandler? Changed;

    public void Load()
    {
        try
        {
            if (!File.Exists(_paths.SettingsFile))
            {
                _log.Information("No settings file, writing defaults to {Path}", _paths.SettingsFile);
                _current = new AppSettings();
                TryWrite(_current);
                return;
            }

            var json = File.ReadAllText(_paths.SettingsFile);
            _current = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
            _log.Debug("Loaded settings from {Path}", _paths.SettingsFile);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Settings file unreadable; restoring defaults");
            QuarantineCorruptFile();
            _current = new AppSettings();
            TryWrite(_current);
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Serialise concurrent saves (nav, window resize and the settings page can
        // all fire at once) so the temp file is never written by two callers.
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.BaseDirectory);
            var json = JsonSerializer.Serialize(settings, SerializerOptions);

            var tmp = _paths.SettingsFile + $".tmp-{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
            MoveWithRetry(tmp, _paths.SettingsFile);

            _current = settings.Clone();
            _log.Debug("Saved settings to {Path}", _paths.SettingsFile);
        }
        finally
        {
            _writeLock.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void MoveWithRetry(string source, string destination)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(60 * attempt); // brief AV / indexer lock on the target
            }
            catch
            {
                try { File.Delete(source); } catch { /* leave the stray temp */ }
                throw;
            }
        }
    }

    private void TryWrite(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_paths.BaseDirectory);
            File.WriteAllText(_paths.SettingsFile,
                JsonSerializer.Serialize(settings, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Could not write default settings file");
        }
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var target = _paths.SettingsFile + $".corrupt-{stamp}";
            File.Move(_paths.SettingsFile, target, overwrite: true);
            _log.Information("Moved corrupt settings file to {Path}", target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Could not quarantine corrupt settings file");
        }
    }
}
