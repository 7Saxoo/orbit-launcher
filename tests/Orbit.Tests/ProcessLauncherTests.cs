using System.ComponentModel;
using System.Diagnostics;
using Orbit.Core.Models;
using Orbit.Core.Services;
using Orbit.Tests.TestSupport;
using Serilog.Core;

namespace Orbit.Tests;

public class ProcessLauncherTests
{
    private readonly FakeExecutableInspector _inspector = new();
    private ProcessStartInfo? _captured;

    private ProcessLauncher Build(Func<ProcessStartInfo, Process?> starter) =>
        new(_inspector, Logger.None, psi =>
        {
            _captured = psi;
            return starter(psi);
        });

    private static AppEntry Entry(string path = @"C:\Program Files\Game\game.exe",
        string? args = null, string? workingDir = null) => new()
    {
        Name = "Game",
        ExecutablePath = path,
        Arguments = args,
        WorkingDirectory = workingDir
    };

    [Fact]
    public void Launch_starts_process_with_shell_execute_and_no_cmd()
    {
        var launcher = Build(_ => null);

        var outcome = launcher.Launch(Entry(args: "--windowed"));

        Assert.Equal(LaunchStatus.Started, outcome.Status);
        Assert.NotNull(_captured);
        Assert.True(_captured!.UseShellExecute);
        Assert.Equal("--windowed", _captured.Arguments);
        Assert.EndsWith("game.exe", _captured.FileName);
        Assert.DoesNotContain("cmd.exe", _captured.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Launch_aborts_when_inspector_reports_missing()
    {
        _inspector.Exists = false;
        var started = false;
        var launcher = Build(_ => { started = true; return null; });

        var outcome = launcher.Launch(Entry());

        Assert.Equal(LaunchStatus.FileNotFound, outcome.Status);
        Assert.False(started);
    }

    [Fact]
    public void Launch_reports_not_an_executable()
    {
        _inspector.HasExe = false;
        var outcome = Build(_ => null).Launch(Entry(@"C:\docs\file.txt"));
        Assert.Equal(LaunchStatus.NotAnExecutable, outcome.Status);
    }

    [Theory]
    [InlineData(5, LaunchStatus.AccessDenied)]
    [InlineData(1223, LaunchStatus.CancelledByUser)]
    [InlineData(2, LaunchStatus.FileNotFound)]
    [InlineData(999, LaunchStatus.Failed)]
    public void Launch_maps_win32_error_codes(int code, LaunchStatus expected)
    {
        var launcher = Build(_ => throw new Win32Exception(code));
        var outcome = launcher.Launch(Entry());
        Assert.Equal(expected, outcome.Status);
        Assert.NotNull(outcome.Error);
    }

    [Fact]
    public void Launch_wraps_unexpected_exceptions_as_failed()
    {
        var launcher = Build(_ => throw new InvalidOperationException("boom"));
        var outcome = launcher.Launch(Entry());
        Assert.Equal(LaunchStatus.Failed, outcome.Status);
    }

    [Fact]
    public void Launch_sets_runas_verb_when_RunAsAdmin()
    {
        var launcher = Build(_ => null);
        var entry = Entry();
        entry.RunAsAdmin = true;

        launcher.Launch(entry);

        Assert.Equal("runas", _captured!.Verb);
    }

    [Theory]
    [InlineData(2048, null, "-Xmx2048M -Xms2048M")]
    [InlineData(1024, "--fullscreen", "-Xmx1024M -Xms1024M --fullscreen")]
    [InlineData(null, "--fullscreen", "--fullscreen")]
    [InlineData(0, "--x", "--x")]
    public void ComposeArguments_prepends_java_heap_when_requested(int? mb, string? userArgs, string expected)
    {
        var entry = Entry(args: userArgs);
        entry.JavaMaxMemoryMb = mb;
        Assert.Equal(expected, ProcessLauncher.ComposeArguments(entry));
    }
}
