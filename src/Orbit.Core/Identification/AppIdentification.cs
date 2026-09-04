using Orbit.Core.Models;

namespace Orbit.Core.Identification;

public enum IdentificationKind
{
    Unknown = 0,
    Application = 1,
    Game = 2
}

/// <summary>Outcome of trying to recognise an executable.</summary>
public sealed record AppIdentification
{
    public required IdentificationKind Kind { get; init; }

    /// <summary>0..1 – below <see cref="AppIdentificationService.MinConfidence"/> the result is treated as Unknown.</summary>
    public required double Confidence { get; init; }

    public string? Name { get; init; }
    public string? Publisher { get; init; }
    public string? Genre { get; init; }

    /// <summary>Local path to a downloaded cover/logo, when one was fetched.</summary>
    public string? CoverImagePath { get; init; }

    /// <summary>Short human explanation, e.g. "Dossier Steam" or "IGDB".</summary>
    public string Source { get; init; } = "Analyse locale";

    public string SuggestedCategory { get; init; } = "Inconnu";

    public AppKind ToAppKind() => Kind == IdentificationKind.Game ? AppKind.Game : AppKind.Application;

    public bool IsReliable => Confidence >= AppIdentificationService.MinConfidence
                              && Kind != IdentificationKind.Unknown;

    public static AppIdentification Unreliable(ExeSignals signals) => new()
    {
        Kind = IdentificationKind.Unknown,
        Confidence = 0,
        Name = signals.BestLocalName,
        Publisher = signals.CompanyName,
        Source = "Non reconnu",
        SuggestedCategory = "Inconnu"
    };
}
