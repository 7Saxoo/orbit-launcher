using Orbit.Core.Services;

namespace Orbit.Core.Identification;

/// <summary>Exposes the identification API keys stored in <see cref="AppSettings"/>.</summary>
public sealed class SettingsIdentificationAdapter : IIdentificationSettings
{
    private readonly ISettingsService _settings;

    public SettingsIdentificationAdapter(ISettingsService settings) => _settings = settings;

    public string? IgdbClientId => Trim(_settings.Current.IgdbClientId);
    public string? IgdbClientSecret => Trim(_settings.Current.IgdbClientSecret);
    public string? SteamGridDbApiKey => Trim(_settings.Current.SteamGridDbApiKey);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
