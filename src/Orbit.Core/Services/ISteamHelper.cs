namespace Orbit.Core.Services;

/// <summary>
/// Helps launch Steam games reliably: resolve a Steam appid from an executable
/// path, and make sure the Steam client is running (started minimised) before a
/// <c>steam://rungameid/…</c> URI is fired.
/// </summary>
public interface ISteamHelper
{
    /// <summary>True when the path lives inside a Steam library's <c>steamapps\common</c>.</summary>
    bool IsSteamGamePath(string executablePath);

    /// <summary>Finds the Steam appid for an executable under a Steam library, or null.</summary>
    string? ResolveAppId(string executablePath);

    /// <summary>
    /// If the Steam client is not already running and can be located, starts it
    /// with <c>-silent</c> (straight to the tray) and waits briefly for it to be
    /// ready. Best effort; never throws.
    /// </summary>
    void EnsureRunningMinimised();
}
