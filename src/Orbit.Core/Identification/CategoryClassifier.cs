using Orbit.Core.Models;

namespace Orbit.Core.Identification;

/// <summary>
/// Assigns a human category (Navigateur, Développement, Multimédia…) from an
/// app's name, publisher and install path. Keyword-based and deliberately
/// conservative: unknown apps fall back to "Applications".
/// </summary>
public static class CategoryClassifier
{
    public const string Games = "Jeux";
    public const string Applications = "Applications";
    public const string Unknown = "Inconnu";

    private static readonly (string Category, string[] Keywords)[] Rules =
    {
        ("Navigateur", new[]
        {
            "chrome", "firefox", "mozilla", "edge", "brave", "opera", "vivaldi", "chromium", "tor browser",
        }),
        ("Développement", new[]
        {
            "visual studio", "vscode", "vs code", "code.exe", "jetbrains", "intellij", "pycharm",
            "webstorm", "rider", "clion", "goland", "phpstorm", "android studio",
            "git.exe", "\\git\\", "git bash", "github", "gitlab", "git for windows",
            "python", "node.js", "nodejs", "docker", "sublime text", "notepad++", "cmake", "mingw",
            "unity hub", "unreal engine", "godot", " sdk", "postman", "insomnia", "dbeaver",
            "sourcetree", "winscp", "mysql", "postgresql", "mariadb", "mongodb",
        }),
        ("Multimédia", new[]
        {
            "vlc", "spotify", "obs", "audacity", "gimp", "photoshop", "lightroom", "premiere", "after effects",
            "davinci resolve", "blender", "krita", "paint.net", "handbrake", "foobar", "musicbee", "itunes",
            "media player", "shotcut", "kdenlive", "inkscape", "figma", "canva", "capcut", "reaper",
        }),
        ("Communication", new[]
        {
            "discord", "slack", "microsoft teams", "teams", "zoom", "skype", "telegram", "whatsapp",
            "signal", "thunderbird", "outlook", "mattermost", "element", "webex",
        }),
        ("Bureautique", new[]
        {
            "word", "excel", "powerpoint", "microsoft office", "office", "libreoffice", "openoffice",
            "onenote", "acrobat", "adobe reader", "adobe acrobat", "foxit", "sumatra", "notion", "obsidian",
            "evernote", "todoist", "onedrive", "dropbox", "google drive",
        }),
        ("Utilitaires", new[]
        {
            "7-zip", "7zip", "winrar", "winzip", "peazip", "ccleaner", "rufus", "etcher", "everything",
            "powertoys", "hwinfo", "cpu-z", "gpu-z", "msi afterburner", "wireshark", "process explorer",
            "autoruns", "sysinternals", "windirstat", "treesize", "revo uninstaller", "driver", "nvidia",
            "amd software", "radeon", "logitech", "razer", "corsair", "steelseries", "backup", "veeam",
        }),
        ("Jeu — launcher", new[]
        {
            "steam", "epic games launcher", "ubisoft connect", "uplay", "ea app", "origin", "battle.net",
            "gog galaxy", "riot client", "rockstar games launcher",
        }),
    };

    private static readonly string[] GamePathMarkers =
    {
        @"\steamapps\common\", @"\steamlibrary\", @"\epic games\", @"\gog galaxy\games\",
        @"\riot games\", @"\ubisoft\", @"\ea games\", @"\origin games\", @"\battle.net\",
    };

    public static string Classify(string? name, string? publisher, string? path, AppKind kind)
    {
        if (kind == AppKind.Game || (path is not null && GamePathMarkers.Any(
                m => path.Contains(m, StringComparison.OrdinalIgnoreCase))))
        {
            return Games;
        }

        var haystack = $" {name} | {publisher} | {path} ".ToLowerInvariant();

        foreach (var (category, keywords) in Rules)
        {
            if (keywords.Any(k => haystack.Contains(k, StringComparison.Ordinal)))
                return category;
        }

        return Applications;
    }
}
