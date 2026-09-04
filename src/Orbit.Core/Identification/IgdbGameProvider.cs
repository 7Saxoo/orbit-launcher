using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Orbit.Core.Infrastructure;
using Serilog;

namespace Orbit.Core.Identification;

/// <summary>
/// Identifies video games through the IGDB API and downloads a cover. Inert
/// unless an IGDB (Twitch) client id + secret are configured in Settings.
/// </summary>
public sealed class IgdbGameProvider : IIdentificationProvider
{
    private readonly HttpClient _http;
    private readonly IIdentificationSettings _settings;
    private readonly OrbitPaths _paths;
    private readonly ILogger _log;

    private string? _token;
    private DateTimeOffset _tokenExpiry;
    private bool _missingKeysLogged;

    public IgdbGameProvider(
        HttpClient http, IIdentificationSettings settings, OrbitPaths paths, ILogger log)
    {
        _http = http;
        _settings = settings;
        _paths = paths;
        _log = log.ForContext<IgdbGameProvider>();
    }

    public int Order => 20;

    public async Task<AppIdentification?> IdentifyAsync(
        ExeSignals signals, AppIdentification? current, CancellationToken ct)
    {
        // Only spend an API call when a game is plausible.
        if (current is { Kind: IdentificationKind.Application, Confidence: >= 0.8 })
            return null;

        var clientId = _settings.IgdbClientId;
        var clientSecret = _settings.IgdbClientSecret;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            if (!_missingKeysLogged)
            {
                _log.Information("IGDB keys not configured – online game identification disabled");
                _missingKeysLogged = true;
            }
            return null;
        }

        var query = current?.Name ?? signals.BestLocalName;
        if (string.IsNullOrWhiteSpace(query))
            return null;

        try
        {
            var token = await EnsureTokenAsync(clientId!, clientSecret!, ct).ConfigureAwait(false);
            if (token is null)
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games");
            request.Headers.Add("Client-ID", clientId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(
                $"search \"{Escape(query)}\"; " +
                "fields name,genres.name,involved_companies.company.name,involved_companies.publisher,cover.image_id; " +
                "limit 6;", Encoding.UTF8, "text/plain");

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("IGDB search failed: {Status}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var best = doc.RootElement.EnumerateArray()
                .Select(g => (Element: g, Score: TextSimilarity.Score(query, GetString(g, "name"))))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (best.Element.ValueKind != JsonValueKind.Object || best.Score < 0.5)
                return null;

            var game = best.Element;
            var name = GetString(game, "name");
            var genre = FirstNested(game, "genres", "name");
            var publisher = Publisher(game);
            var coverPath = await DownloadCoverAsync(game, name, ct).ConfigureAwait(false);

            return new AppIdentification
            {
                Kind = IdentificationKind.Game,
                Confidence = Math.Min(0.97, 0.6 + best.Score * 0.4),
                Name = name,
                Publisher = publisher ?? current?.Publisher,
                Genre = genre,
                CoverImagePath = coverPath,
                Source = "IGDB",
                SuggestedCategory = "Jeux"
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _log.Warning(ex, "IGDB lookup failed for {Query}", query);
            return null;
        }
    }

    private async Task<string?> EnsureTokenAsync(string clientId, string secret, CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _token;

        var url = $"https://id.twitch.tv/oauth2/token?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&client_secret={Uri.EscapeDataString(secret)}&grant_type=client_credentials";

        using var response = await _http.PostAsync(url, content: null, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _log.Warning("IGDB token request failed: {Status}", response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        _token = doc.RootElement.GetProperty("access_token").GetString();
        var seconds = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt64() : 3600;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(seconds - 120);
        return _token;
    }

    private async Task<string?> DownloadCoverAsync(JsonElement game, string? name, CancellationToken ct)
    {
        if (!game.TryGetProperty("cover", out var cover) ||
            cover.ValueKind != JsonValueKind.Object ||
            !cover.TryGetProperty("image_id", out var imageId))
        {
            return null;
        }

        var id = imageId.GetString();
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var url = $"https://images.igdb.com/igdb/image/upload/t_cover_big/{id}.jpg";
        var target = Path.Combine(_paths.CoverCacheDirectory,
            PathHelper.StableToken(name ?? id) + ".jpg");

        try
        {
            Directory.CreateDirectory(_paths.CoverCacheDirectory);
            var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(target, bytes, ct).ConfigureAwait(false);
            return target;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            _log.Debug(ex, "Cover download failed for {Name}", name);
            return null;
        }
    }

    private static string Escape(string value) => value.Replace("\"", "\\\"");

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FirstNested(JsonElement element, string arrayProp, string innerProp)
    {
        if (!element.TryGetProperty(arrayProp, out var array) || array.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var item in array.EnumerateArray())
            if (GetString(item, innerProp) is { } value)
                return value;
        return null;
    }

    private static string? Publisher(JsonElement game)
    {
        if (!game.TryGetProperty("involved_companies", out var companies) ||
            companies.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? firstCompany = null;
        foreach (var involved in companies.EnumerateArray())
        {
            var companyName = involved.TryGetProperty("company", out var c) ? GetString(c, "name") : null;
            firstCompany ??= companyName;
            if (involved.TryGetProperty("publisher", out var isPub) &&
                isPub.ValueKind == JsonValueKind.True && companyName is not null)
            {
                return companyName;
            }
        }

        return firstCompany;
    }
}
