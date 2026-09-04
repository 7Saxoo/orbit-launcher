using Orbit.Core.Infrastructure;
using Serilog;

namespace Orbit.Core.Identification;

/// <summary>
/// Confirms/enriches an "application" guess by looking the name up in the
/// Windows Package Manager catalogue (<c>winget search</c>). Never downgrades a
/// confident game result.
/// </summary>
public sealed class WinGetProvider : IIdentificationProvider
{
    private readonly IProcessRunner _runner;
    private readonly ILogger _log;
    private bool _wingetMissingLogged;

    public WinGetProvider(IProcessRunner runner, ILogger log)
    {
        _runner = runner;
        _log = log.ForContext<WinGetProvider>();
    }

    public int Order => 10;

    public async Task<AppIdentification?> IdentifyAsync(
        ExeSignals signals, AppIdentification? current, CancellationToken ct)
    {
        // Skip when we're already confident it's a game.
        if (current is { Kind: IdentificationKind.Game, Confidence: >= 0.8 })
            return null;

        var query = TextSimilarity.Normalize(current?.Name ?? signals.BestLocalName);
        if (query.Length < 3)
            return null;

        string stdout;
        try
        {
            var result = await _runner.RunAsync(
                "winget",
                $"search --name \"{query}\" --source winget --accept-source-agreements --disable-interactivity",
                TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
            stdout = result.StandardOutput;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.Debug("winget search timed out for {Query}", query);
            return null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            if (!_wingetMissingLogged)
            {
                _log.Information("winget not available – skipping application catalogue lookup");
                _wingetMissingLogged = true;
            }
            return null;
        }

        var packages = WinGetOutputParser.Parse(stdout);
        if (packages.Count == 0)
            return null;

        var best = packages
            .Select(p => (Package: p, Score: TextSimilarity.Score(query, p.Name)))
            .OrderByDescending(x => x.Score)
            .First();

        if (best.Score < 0.55)
            return null;

        var publisherFromId = best.Package.Id.Contains('.')
            ? best.Package.Id[..best.Package.Id.IndexOf('.')]
            : null;

        return new AppIdentification
        {
            Kind = IdentificationKind.Application,
            Confidence = Math.Min(0.95, 0.55 + best.Score * 0.4),
            Name = best.Package.Name.Trim(),
            Publisher = current?.Publisher ?? publisherFromId,
            Source = $"WinGet : {best.Package.Id}",
            SuggestedCategory = "Applications"
        };
    }
}
