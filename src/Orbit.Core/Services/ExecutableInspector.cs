using System.Diagnostics;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;

namespace Orbit.Core.Services;

/// <inheritdoc />
public sealed class ExecutableInspector : IExecutableInspector
{
    public ExecutableInfo Inspect(string path)
    {
        var normalized = PathHelper.Normalize(path);
        var hasExe = PathHelper.HasExecutableExtension(normalized);
        var exists = false;
        try
        {
            exists = File.Exists(normalized);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Treat an unreadable path as "missing" rather than crashing the add flow.
            exists = false;
        }

        string? product = null, company = null, description = null, version = null;
        if (exists)
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(normalized);
                product = NullIfBlank(vi.ProductName);
                company = NullIfBlank(vi.CompanyName);
                description = NullIfBlank(vi.FileDescription);
                version = NullIfBlank(vi.FileVersion);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
            {
                // Metadata is optional; ignore and fall back to the file name.
            }
        }

        var suggested = description
            ?? product
            ?? (normalized.Length > 0
                ? Path.GetFileNameWithoutExtension(normalized)
                : null);

        return new ExecutableInfo
        {
            NormalizedPath = normalized,
            Exists = exists,
            HasExeExtension = hasExe,
            SuggestedName = NullIfBlank(suggested),
            ProductName = product,
            CompanyName = company,
            FileDescription = description,
            FileVersion = version
        };
    }

    public AppAvailability Evaluate(string path)
    {
        var normalized = PathHelper.Normalize(path);
        if (!PathHelper.HasExecutableExtension(normalized))
            return AppAvailability.Invalid;

        try
        {
            return File.Exists(normalized) ? AppAvailability.Available : AppAvailability.Missing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AppAvailability.Missing;
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
