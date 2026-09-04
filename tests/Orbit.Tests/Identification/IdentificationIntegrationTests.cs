using Orbit.Core.Identification;
using Orbit.Core.Infrastructure;
using Serilog.Core;

namespace Orbit.Tests.Identification;

/// <summary>
/// Exercises the real identification pipeline (local heuristics + the actual
/// <c>winget</c> executable) against files that exist on every Windows box.
/// Network-dependent, so failures here are tolerated on machines without winget.
/// </summary>
public class IdentificationIntegrationTests
{
    private static AppIdentificationService Build() => new(
        new IIdentificationProvider[]
        {
            new PathHeuristicsProvider(),
            new WinGetProvider(new ProcessRunner(), Logger.None)
        },
        Logger.None);

    [Fact]
    public async Task Identifies_a_system_executable_without_throwing()
    {
        var notepad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        if (!File.Exists(notepad))
            return; // nothing to test on this box

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var result = await Build().IdentifyAsync(notepad, cts.Token);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
        // notepad lives in System32 – never a game.
        Assert.NotEqual(IdentificationKind.Game, result.Kind);
    }

    [Fact]
    public async Task Unknown_path_stays_unknown_end_to_end()
    {
        var fake = Path.Combine(Path.GetTempPath(), $"orbit-id-{Guid.NewGuid():N}.exe");
        await File.WriteAllBytesAsync(fake, new byte[64]);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            var result = await Build().IdentifyAsync(fake, cts.Token);
            Assert.Equal(IdentificationKind.Unknown, result.Kind);
        }
        finally
        {
            File.Delete(fake);
        }
    }
}
