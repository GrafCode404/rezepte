using System.Text;

namespace RezepteWeb.Services;

public static class RecipeParser
{
    public static string Normalize(string markdown) => markdown.Replace("\r\n", "\n");

    public static Dictionary<string, string> ParseFrontmatter(string markdown)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = Normalize(markdown);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return result;
        }

        var lines = normalized.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line == "---")
            {
                break;
            }
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }
            result[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return result;
    }

    public static bool IsDeleted(string markdown)
    {
        return ParseFrontmatter(markdown).TryGetValue("deleted", out var value) &&
               (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("1", StringComparison.OrdinalIgnoreCase));
    }

    public static string StripFrontmatter(string markdown)
    {
        var normalized = Normalize(markdown);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return markdown;
        }

        var lines = normalized.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                return string.Join('\n', lines[(i + 1)..]);
            }
        }

        return markdown;
    }

    public static string MarkDeleted(string markdown)
    {
        var normalized = Normalize(markdown);
        if (IsDeleted(normalized))
        {
            return normalized;
        }
        return "---\ndeleted: true\n---\n" + normalized;
    }

    public static string MarkRestored(string markdown) => StripFrontmatter(markdown);

    public static string Slugify(string title)
    {
        var normalized = title
            .ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

        var slug = new StringBuilder(normalized.Length);
        var lastWasDash = false;
        foreach (var c in normalized)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                slug.Append('-');
                lastWasDash = true;
            }
        }

        return slug.ToString().Trim('-');
    }

    public static string ParseTitle(string markdown, string fallback)
    {
        var firstLine = markdown.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("# "));
        return firstLine is null ? fallback : firstLine.TrimStart()[2..].Trim();
    }

    public static Dictionary<string, string> ParseFacts(string markdown)
    {
        var facts = new Dictionary<string, string>();
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("* **"))
            {
                continue;
            }

            var endOfKey = trimmed.IndexOf(":**");
            if (endOfKey < 4)
            {
                continue;
            }

            var key = trimmed[4..endOfKey];
            var value = trimmed[(endOfKey + 3)..].Trim().TrimEnd('*');
            facts[key] = value;
        }

        return facts;
    }

    public static string ExtractIngredients(string markdown)
    {
        var names = new List<string>();
        var inTable = false;
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("|"))
            {
                if (inTable)
                {
                    break;
                }
                continue;
            }

            if (!inTable && trimmed.Contains("Zutaten"))
            {
                inTable = true;
                continue;
            }

            if (!inTable)
            {
                continue;
            }

            var cells = trimmed.Split('|');
            if (cells.Length < 3)
            {
                continue;
            }

            var first = cells[1].Trim();
            if (first.StartsWith(":") || IsSeparatorRow(first))
            {
                continue;
            }

            var name = first.Replace("*", string.Empty).Trim();
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }

        return string.Join(' ', names);
    }

    public static bool IsSeparatorRow(string cell)
    {
        if (cell.Length == 0)
        {
            return true;
        }
        for (var i = 0; i < cell.Length; i++)
        {
            var c = cell[i];
            if (c != '-' && c != ':' && c != ' ')
            {
                return false;
            }
        }
        return true;
    }

    public static bool ContainsWord(string text, string term)
    {
        var index = 0;
        while (index + term.Length <= text.Length)
        {
            index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var after = index + term.Length >= text.Length ||
                        !char.IsLetterOrDigit(text[index + term.Length]);
            if (before && after)
            {
                return true;
            }
            index += term.Length;
        }
        return false;
    }

    public static string RemoveHeaderBlock(string markdown, string title)
    {
        var lines = new List<string>();
        var titleLineFound = false;

        foreach (var line in markdown.Split('\n'))
        {
            if (!titleLineFound && line.TrimStart().StartsWith("# " + title, StringComparison.OrdinalIgnoreCase))
            {
                titleLineFound = true;
                continue;
            }

            if (line.TrimStart().StartsWith("* **"))
            {
                continue;
            }

            lines.Add(line);
        }

        return string.Join('\n', lines);
    }
}
