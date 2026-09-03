using System.Text;

namespace Orbit.Core.Detection;

/// <summary>A parsed node of a Valve KeyValues (VDF) document: either a leaf
/// with a <see cref="Value"/> or an object with <see cref="Children"/>.</summary>
public sealed class VdfNode
{
    public string? Value { get; set; }
    public Dictionary<string, VdfNode> Children { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public VdfNode? this[string key] =>
        Children.TryGetValue(key, out var node) ? node : null;

    public string? ValueOf(string key) => this[key]?.Value;
}

/// <summary>
/// Minimal, dependency-free reader for the subset of the Valve KeyValues format
/// used by <c>libraryfolders.vdf</c> and <c>appmanifest_*.acf</c>: quoted keys
/// and values, nested <c>{ }</c> blocks, <c>//</c> line comments and <c>\"</c>
/// escapes. Unknown constructs are skipped rather than throwing.
/// </summary>
public static class VdfParser
{
    public static VdfNode Parse(string text)
    {
        var root = new VdfNode();
        var pos = 0;
        ParseBody(text, ref pos, root);
        return root;
    }

    private static void ParseBody(string s, ref int pos, VdfNode current)
    {
        while (true)
        {
            SkipTrivia(s, ref pos);
            if (pos >= s.Length)
                return;

            if (s[pos] == '}')
            {
                pos++;
                return;
            }

            var key = ReadToken(s, ref pos);
            if (key is null)
                return;

            SkipTrivia(s, ref pos);
            if (pos >= s.Length)
                return;

            if (s[pos] == '{')
            {
                pos++;
                var child = new VdfNode();
                ParseBody(s, ref pos, child);
                current.Children[key] = child;
            }
            else
            {
                var value = ReadToken(s, ref pos);
                current.Children[key] = new VdfNode { Value = value ?? string.Empty };
            }
        }
    }

    private static void SkipTrivia(string s, ref int pos)
    {
        while (pos < s.Length)
        {
            var c = s[pos];
            if (char.IsWhiteSpace(c))
            {
                pos++;
            }
            else if (c == '/' && pos + 1 < s.Length && s[pos + 1] == '/')
            {
                while (pos < s.Length && s[pos] != '\n')
                    pos++;
            }
            else
            {
                return;
            }
        }
    }

    private static string? ReadToken(string s, ref int pos)
    {
        SkipTrivia(s, ref pos);
        if (pos >= s.Length)
            return null;

        if (s[pos] != '"')
        {
            // Unquoted token (rare in these files) – read to next whitespace/brace.
            var start = pos;
            while (pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != '{' && s[pos] != '}')
                pos++;
            return pos > start ? s[start..pos] : null;
        }

        pos++; // opening quote
        var sb = new StringBuilder();
        while (pos < s.Length)
        {
            var c = s[pos++];
            if (c == '\\' && pos < s.Length)
            {
                var next = s[pos++];
                sb.Append(next switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _ => next
                });
            }
            else if (c == '"')
            {
                return sb.ToString();
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
