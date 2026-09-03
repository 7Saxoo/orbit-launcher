using Orbit.Core.Models;

namespace Orbit.Core.Services;

/// <summary>Inspects executable paths on disk. Abstracted so the library
/// service can be unit-tested without touching the real file system.</summary>
public interface IExecutableInspector
{
    /// <summary>Reads existence, extension and (best effort) version metadata.</summary>
    ExecutableInfo Inspect(string path);

    /// <summary>Cheap check used when refreshing the library view.</summary>
    AppAvailability Evaluate(string path);
}
