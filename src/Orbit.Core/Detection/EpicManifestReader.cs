using System.Text.Json;
using Orbit.Core.Models;

namespace Orbit.Core.Detection;

/// <summary>Parses an Epic Games Launcher <c>.item</c> manifest (plain JSON).</summary>
public static class EpicManifestReader
{
    public static DetectedApp? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var name = GetString(root, "DisplayName");
            var installLocation = GetString(root, "InstallLocation");
            var launchExe = GetString(root, "LaunchExecutable");

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(installLocation) ||
                string.IsNullOrWhiteSpace(launchExe))
            {
                return null;
            }

            var exePath = Path.GetFullPath(Path.Combine(installLocation, launchExe));
            if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return null;

            return new DetectedApp
            {
                Name = name.Trim(),
                ExecutablePath = exePath,
                Kind = AppKind.Game,
                Category = "Epic Games",
                Source = "Epic Games",
                InstallLocation = installLocation,
                Publisher = GetString(root, "DeveloperName")
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
