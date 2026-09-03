using System.Diagnostics;
using Orbit.Core.Models;
using Orbit.Core.Services;

namespace Orbit.Tests;

/// <summary>Real file-system tests – no fakes here.</summary>
public class ExecutableInspectorTests
{
    private readonly ExecutableInspector _inspector = new();

    private static string ThisProcessExe =>
        Process.GetCurrentProcess().MainModule!.FileName;

    [Fact]
    public void Evaluate_returns_Missing_for_a_nonexistent_path()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
        Assert.Equal(AppAvailability.Missing, _inspector.Evaluate(path));
    }

    [Fact]
    public void Evaluate_returns_Invalid_for_a_non_exe_file()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "hello");
        try
        {
            Assert.Equal(AppAvailability.Invalid, _inspector.Evaluate(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Inspect_reads_version_metadata_from_a_real_executable()
    {
        var info = _inspector.Inspect(ThisProcessExe);

        Assert.True(info.Exists);
        Assert.True(info.HasExeExtension);
        Assert.True(info.IsRegisterable);
        Assert.Equal(AppAvailability.Available, info.Availability);
        Assert.False(string.IsNullOrWhiteSpace(info.SuggestedName));
    }
}
