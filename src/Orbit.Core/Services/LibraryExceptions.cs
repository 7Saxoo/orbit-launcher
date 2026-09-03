namespace Orbit.Core.Services;

/// <summary>Base type for expected, user-presentable library errors.</summary>
public abstract class LibraryException : Exception
{
    protected LibraryException(string message) : base(message) { }
}

/// <summary>Raised when adding an executable that is already in the library.</summary>
public sealed class DuplicateAppException : LibraryException
{
    public DuplicateAppException(string path)
        : base($"Cette application est déjà dans la bibliothèque :\n{path}") { }
}

/// <summary>Raised when the chosen file cannot be registered (missing or not an .exe).</summary>
public sealed class ExecutableNotRegisterableException : LibraryException
{
    public ExecutableNotRegisterableException(string message) : base(message) { }
}
