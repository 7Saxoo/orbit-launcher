namespace Orbit.Core.Identification;

/// <summary>Cheap fuzzy string matching used to score identification candidates.</summary>
public static class TextSimilarity
{
    /// <summary>0..1 similarity of two names after normalisation (case, spaces, punctuation, common noise words).</summary>
    public static double Score(string? a, string? b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        if (na.Length == 0 || nb.Length == 0)
            return 0;
        if (na == nb)
            return 1;

        if (na.Contains(nb, StringComparison.Ordinal) || nb.Contains(na, StringComparison.Ordinal))
            return 0.85;

        var distance = Levenshtein(na, nb);
        var longest = Math.Max(na.Length, nb.Length);
        var ratio = 1.0 - (double)distance / longest;
        return Math.Max(0, ratio);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var lower = value.ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != ' ')
                sb.Append(' ');
        }

        var tokens = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !NoiseWords.Contains(t));
        return string.Join(' ', tokens).Trim();
    }

    private static readonly HashSet<string> NoiseWords = new(StringComparer.Ordinal)
    {
        "the", "game", "launcher", "play", "win64", "win32", "x64", "x86",
        "shipping", "app", "edition", "bin", "client", "exe"
    };

    private static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
        }

        return d[a.Length, b.Length];
    }
}
