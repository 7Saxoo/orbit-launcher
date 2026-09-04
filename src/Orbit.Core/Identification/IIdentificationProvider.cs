namespace Orbit.Core.Identification;

/// <summary>
/// A single strategy for recognising an executable (local heuristics, WinGet,
/// IGDB…). Returns null when it has nothing useful to add. Ordered by
/// <see cref="Order"/> – lower runs first, and later providers may use the
/// running best guess (passed as <paramref name="current"/>).
/// </summary>
public interface IIdentificationProvider
{
    int Order { get; }

    Task<AppIdentification?> IdentifyAsync(
        ExeSignals signals,
        AppIdentification? current,
        CancellationToken ct);
}

/// <summary>Optional online-lookup credentials, supplied by the app's settings.
/// When a key is absent the corresponding provider stays inert.</summary>
public interface IIdentificationSettings
{
    string? IgdbClientId { get; }
    string? IgdbClientSecret { get; }
    string? SteamGridDbApiKey { get; }
}
