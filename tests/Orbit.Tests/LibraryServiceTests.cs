using Orbit.Core.Detection;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Orbit.Tests.TestSupport;
using Serilog.Core;

namespace Orbit.Tests;

public class LibraryServiceTests
{
    private readonly FakeAppRepository _repo = new();
    private readonly FakeExecutableInspector _inspector = new();
    private readonly FakeIconService _icons = new();
    private readonly FakeProcessLauncher _launcher = new();
    private readonly LibraryService _service;

    public LibraryServiceTests()
    {
        _service = new LibraryService(_repo, _inspector, _icons, _launcher,
            new OrbitPaths(Path.Combine(Path.GetTempPath(), "orbit-lib-tests", Guid.NewGuid().ToString("N"))),
            Logger.None);
    }

    private static NewAppRequest Request(string path = @"C:\Program Files\Demo\demo.exe", string? name = null) =>
        new() { ExecutablePath = path, Name = name, Kind = AppKind.Application, Category = "Test" };

    [Fact]
    public async Task AddAsync_registers_entry_and_extracts_icon()
    {
        _inspector.SuggestedName = "Demo Suite";
        _icons.Result = @"C:\cache\demo.png";

        var entry = await _service.AddAsync(Request(name: null));

        Assert.Equal("Demo Suite", entry.Name);
        Assert.Equal(@"C:\cache\demo.png", entry.IconCachePath);
        Assert.Single(_repo.Snapshot);
        Assert.Contains(_icons.Requests, r => r.Contains("demo.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AddAsync_prefers_explicit_name_over_suggestion()
    {
        _inspector.SuggestedName = "Ignored";
        var entry = await _service.AddAsync(Request(name: "  My Name  "));
        Assert.Equal("My Name", entry.Name);
    }

    [Fact]
    public async Task AddAsync_throws_when_file_missing()
    {
        _inspector.Exists = false;
        await Assert.ThrowsAsync<ExecutableNotRegisterableException>(() => _service.AddAsync(Request()));
        Assert.Empty(_repo.Snapshot);
    }

    [Fact]
    public async Task AddAsync_throws_when_not_an_exe()
    {
        _inspector.HasExe = false;
        await Assert.ThrowsAsync<ExecutableNotRegisterableException>(() => _service.AddAsync(Request()));
    }

    [Fact]
    public async Task AddAsync_rejects_duplicates()
    {
        await _service.AddAsync(Request());
        await Assert.ThrowsAsync<DuplicateAppException>(() => _service.AddAsync(Request()));
        Assert.Single(_repo.Snapshot);
    }

    [Fact]
    public async Task LoadAsync_reports_missing_availability()
    {
        await _service.AddAsync(Request());
        _inspector.Exists = false; // the file "disappears"

        var items = await _service.LoadAsync();

        Assert.Single(items);
        Assert.Equal(AppAvailability.Missing, items[0].Availability);
    }

    [Fact]
    public async Task LaunchAsync_records_launch_only_on_success()
    {
        var entry = await _service.AddAsync(Request());

        _launcher.NextOutcome = LaunchOutcome.Ok("demo.exe");
        var ok = await _service.LaunchAsync(entry.Id);
        Assert.True(ok.Succeeded);
        Assert.Equal(1, _repo.Snapshot.Single().LaunchCount);

        _launcher.NextOutcome = new LaunchOutcome(LaunchStatus.AccessDenied, "nope");
        var denied = await _service.LaunchAsync(entry.Id);
        Assert.False(denied.Succeeded);
        Assert.Equal(1, _repo.Snapshot.Single().LaunchCount); // unchanged
    }

    [Fact]
    public async Task RemoveAsync_deletes_entry_without_any_file_io()
    {
        var entry = await _service.AddAsync(Request());
        await _service.RemoveAsync(entry.Id);
        Assert.Empty(_repo.Snapshot);
    }

    [Fact]
    public async Task SetFavoriteAsync_toggles_flag()
    {
        var entry = await _service.AddAsync(Request());
        var updated = await _service.SetFavoriteAsync(entry.Id, true);
        Assert.True(updated.IsFavorite);
        Assert.True(_repo.Snapshot.Single().IsFavorite);
    }

    [Fact]
    public async Task UpdateAsync_refreshes_icon_when_path_changes_and_keeps_stats()
    {
        var entry = await _service.AddAsync(Request());
        await _service.LaunchAsync(entry.Id); // LaunchCount = 1

        _icons.Requests.Clear();
        var edited = _repo.Snapshot.Single().Clone();
        edited.ExecutablePath = @"C:\Program Files\Demo\demo2.exe";
        edited.Name = "Renamed";

        var result = await _service.UpdateAsync(edited);

        Assert.Equal("Renamed", result.Name);
        Assert.Equal(1, _repo.Snapshot.Single().LaunchCount); // preserved
        Assert.NotEmpty(_icons.Requests); // icon re-extracted for the new path
    }

    [Fact]
    public async Task UpdateAsync_does_not_reextract_icon_when_path_unchanged()
    {
        var entry = await _service.AddAsync(Request());
        _icons.Requests.Clear();

        var edited = _repo.Snapshot.Single().Clone();
        edited.Name = "Just a rename";
        await _service.UpdateAsync(edited);

        Assert.Empty(_icons.Requests);
    }

    [Fact]
    public async Task ResetAsync_clears_the_repository()
    {
        await _service.AddAsync(Request(@"C:\a\a.exe"));
        await _service.AddAsync(Request(@"C:\b\b.exe"));

        await _service.ResetAsync();

        Assert.Empty(_repo.Snapshot);
        Assert.Equal(1, _repo.DeleteAllCallCount);
    }

    [Fact]
    public async Task ImportAsync_adds_new_entries_and_skips_duplicates()
    {
        await _service.AddAsync(Request(@"C:\Games\Existing\game.exe", "Existing"));

        var detected = new[]
        {
            new DetectedApp { Name = "Fresh One", ExecutablePath = @"C:\Games\Fresh1\a.exe", Kind = AppKind.Game, Source = "Steam" },
            new DetectedApp { Name = "Fresh Two", ExecutablePath = @"C:\Games\Fresh2\b.exe", Kind = AppKind.Game, Source = "Steam" },
            new DetectedApp { Name = "Existing again", ExecutablePath = @"C:\Games\Existing\game.exe", Kind = AppKind.Game, Source = "Steam" },
        };

        var added = await _service.ImportAsync(detected);

        Assert.Equal(2, added);
        Assert.Equal(3, _repo.Snapshot.Count);
    }

    [Fact]
    public async Task ImportAsync_skips_entries_that_do_not_exist()
    {
        _inspector.Exists = false; // every candidate is "missing"

        var added = await _service.ImportAsync(new[]
        {
            new DetectedApp { Name = "Ghost", ExecutablePath = @"C:\nope\ghost.exe", Source = "Steam" }
        });

        Assert.Equal(0, added);
        Assert.Empty(_repo.Snapshot);
    }

    [Fact]
    public async Task IsRunning_delegates_to_launcher()
    {
        var entry = await _service.AddAsync(Request());
        _launcher.Running = true;
        Assert.True(_service.IsRunning(_repo.Snapshot.Single()));
    }
}
