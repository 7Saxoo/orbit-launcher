using System.IO;
using Orbit.Core.Infrastructure;
using Serilog;
using Serilog.Events;

namespace Orbit.App.Infrastructure;

/// <summary>Builds the application's Serilog logger: a rolling daily file under
/// <c>%LOCALAPPDATA%\Orbit\logs</c> plus the debugger output window.</summary>
public static class OrbitLogging
{
    public static ILogger Create(OrbitPaths paths)
    {
        Directory.CreateDirectory(paths.LogDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Debug(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(paths.LogDirectory, "orbit-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
