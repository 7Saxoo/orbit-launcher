namespace Orbit.Core.Identification;

/// <summary>
/// Offline first pass: classifies an executable from where it lives and its
/// version metadata. Always returns a result (possibly Unknown) so later
/// online providers have a name to search with.
/// </summary>
public sealed class PathHeuristicsProvider : IIdentificationProvider
{
    public int Order => 0;

    private static readonly string[] GameStoreMarkers =
    {
        @"steamapps\common", @"\steamlibrary\", @"\gog galaxy\games\",
        @"\epic games\", @"\riot games\", @"\ubisoft\", @"\origin games\",
        @"\ea games\", @"\battle.net\", @"\rockstar games\"
    };

    private static readonly string[] LauncherNames =
    {
        "launcher", "crashhandler", "crashpad", "unitycrashhandler", "unins",
        "setup", "update", "installer", "helper", "service", "cleaner"
    };

    private static readonly string[] AppPublishers =
    {
        "microsoft", "google", "mozilla", "adobe", "jetbrains", "valve corporation",
        "discord", "spotify", "oracle", "notepad++", "obs", "vlc", "7-zip",
        "python software foundation", "git", "docker"
    };

    public Task<AppIdentification?> IdentifyAsync(ExeSignals s, AppIdentification? current, CancellationToken ct)
    {
        var name = s.BestLocalName;
        var lowerName = s.FileNameNoExt.ToLowerInvariant();
        var isLauncherName = LauncherNames.Any(n => lowerName.Contains(n, StringComparison.Ordinal));

        // ---- Strong "game" signals: sitting in a known store's game folder ----
        if (GameStoreMarkers.Any(s.PathContains))
        {
            var store = StoreLabel(s);
            return Result(new AppIdentification
            {
                Kind = IdentificationKind.Game,
                Confidence = isLauncherName ? 0.55 : 0.82,
                Name = name,
                Publisher = s.CompanyName,
                Source = store,
                SuggestedCategory = "Jeux"
            });
        }

        // ---- Windows Store / packaged apps ----
        if (s.PathContains(@"\windowsapps\") || s.PathContains(@"\systemapps\"))
        {
            return Result(new AppIdentification
            {
                Kind = IdentificationKind.Application,
                Confidence = 0.7,
                Name = name,
                Publisher = s.CompanyName,
                Source = "Application Windows (MSIX)",
                SuggestedCategory = "Applications"
            });
        }

        // ---- Known application publishers in the version block ----
        var company = s.CompanyName?.ToLowerInvariant() ?? string.Empty;
        if (AppPublishers.Any(p => company.Contains(p, StringComparison.Ordinal)))
        {
            return Result(new AppIdentification
            {
                Kind = IdentificationKind.Application,
                Confidence = 0.6,
                Name = name,
                Publisher = s.CompanyName,
                Source = $"Éditeur : {s.CompanyName}",
                SuggestedCategory = "Applications"
            });
        }

        // ---- Generic Program Files install with metadata → likely an app ----
        if ((s.PathContains(@"\program files\") || s.PathContains(@"\program files (x86)\"))
            && !string.IsNullOrWhiteSpace(s.CompanyName) && !isLauncherName)
        {
            return Result(new AppIdentification
            {
                Kind = IdentificationKind.Application,
                Confidence = 0.5,
                Name = name,
                Publisher = s.CompanyName,
                Source = "Installée dans Program Files",
                SuggestedCategory = "Applications"
            });
        }

        // ---- Nothing conclusive ----
        return Result(new AppIdentification
        {
            Kind = IdentificationKind.Unknown,
            Confidence = 0.2,
            Name = name,
            Publisher = s.CompanyName,
            Source = "Analyse locale",
            SuggestedCategory = "Inconnu"
        });
    }

    private static string StoreLabel(ExeSignals s) =>
        s.PathContains("steamapps") || s.PathContains("steamlibrary") ? "Dossier Steam" :
        s.PathContains("gog galaxy") ? "Dossier GOG" :
        s.PathContains("epic games") ? "Dossier Epic Games" :
        s.PathContains("riot games") ? "Dossier Riot" :
        s.PathContains("ubisoft") ? "Dossier Ubisoft" :
        "Dossier de jeux";

    private static Task<AppIdentification?> Result(AppIdentification value) =>
        Task.FromResult<AppIdentification?>(value);
}
