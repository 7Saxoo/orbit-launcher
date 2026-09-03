using Orbit.Core.Infrastructure;

namespace Orbit.Tests.TestSupport;

/// <summary>A throwaway directory under the system temp folder, wired into an
/// <see cref="OrbitPaths"/>. Deleted on dispose.</summary>
public sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "orbit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new OrbitPaths(Root);
        Paths.EnsureDirectories();
    }

    public string Root { get; }
    public OrbitPaths Paths { get; }

    /// <summary>Creates a real, tiny file at the given relative path and returns its full path.</summary>
    public string CreateFile(string relativePath, byte[]? content = null)
    {
        var full = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content ?? new byte[] { 0x4D, 0x5A }); // "MZ"
        return full;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A lingering file handle (e.g. SQLite WAL) – best effort only.
        }
    }
}
