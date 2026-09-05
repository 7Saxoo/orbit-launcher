using Serilog;

namespace Orbit.Core.Identification;

/// <inheritdoc />
public sealed class AppIdentificationService : IAppIdentificationService
{
    /// <summary>Results below this confidence are reported as "Inconnu".</summary>
    public const double MinConfidence = 0.45;

    private readonly IReadOnlyList<IIdentificationProvider> _providers;
    private readonly ILogger _log;

    public AppIdentificationService(IEnumerable<IIdentificationProvider> providers, ILogger log)
    {
        _providers = providers.OrderBy(p => p.Order).ToList();
        _log = log.ForContext<AppIdentificationService>();
    }

    public async Task<AppIdentification> IdentifyAsync(string executablePath, CancellationToken ct = default)
    {
        var signals = ExeSignals.Extract(executablePath);
        AppIdentification? best = null;

        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await provider.IdentifyAsync(signals, best, ct).ConfigureAwait(false);
                if (result is null)
                    continue;

                if (best is null || result.Confidence > best.Confidence)
                    best = result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Identification provider {Provider} failed", provider.GetType().Name);
            }
        }

        if (best is null || best.Confidence < MinConfidence || best.Kind == IdentificationKind.Unknown)
        {
            _log.Information("Executable {File} not confidently identified (best {Score:P0})",
                signals.FileName, best?.Confidence ?? 0);
            return AppIdentification.Unreliable(signals) with
            {
                Name = best?.Name ?? signals.BestLocalName,
                Publisher = best?.Publisher ?? signals.CompanyName
            };
        }

        // Refine a generic "Applications" into a concrete category.
        var category = CategoryClassifier.Classify(
            best.Name, best.Publisher, signals.NormalizedPath, best.ToAppKind());

        _log.Information("Identified {File} as {Kind} '{Name}' [{Category}] ({Score:P0}, {Source})",
            signals.FileName, best.Kind, best.Name, category, best.Confidence, best.Source);
        return best with { SuggestedCategory = category };
    }
}
