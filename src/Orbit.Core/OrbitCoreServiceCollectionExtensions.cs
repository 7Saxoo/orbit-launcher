using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Data;
using Orbit.Core.Detection;
using Orbit.Core.Infrastructure;
using Orbit.Core.Services;

namespace Orbit.Core;

/// <summary>Registers the Core services. The host is responsible for having a
/// <see cref="Serilog.ILogger"/> already registered in the container.</summary>
public static class OrbitCoreServiceCollectionExtensions
{
    public static IServiceCollection AddOrbitCore(this IServiceCollection services, OrbitPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(paths);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<IAppRepository, SqliteAppRepository>();
        services.AddSingleton<IExecutableInspector, ExecutableInspector>();
        services.AddSingleton<IIconService, IconService>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<ILibraryService, LibraryService>();

        // Installed-software detection sources + aggregator.
        services.AddSingleton<IInstalledAppSource, RegistryUninstallSource>();
        services.AddSingleton<IInstalledAppSource, SteamSource>();
        services.AddSingleton<IInstalledAppSource, EpicGamesSource>();
        services.AddSingleton<IAppDetectionService, AppDetectionService>();

        return services;
    }
}
