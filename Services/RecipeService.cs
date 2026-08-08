using System.Text;
using Markdig;
using RezepteWeb.Models;

namespace RezepteWeb.Services;

public class RecipeService
{
    private readonly List<Recipe> _recipes;

    public RecipeService(string recipeDirectory)
    {
        _recipes = Load(recipeDirectory);
    }

    public IReadOnlyList<Recipe> GetAll() => _recipes;

    public Recipe? GetBySlug(string slug)
        => _recipes.FirstOrDefault(r => r.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Recipe> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _recipes;
        }

        var needle = query.Trim();
        return _recipes.Where(r =>
            r.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            r.SearchText.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static List<Recipe> Load(string directory)
    {
        var recipes = new List<Recipe>();
        if (!Directory.Exists(directory))
        {
            return recipes;
        }

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        foreach (var file in Directory.EnumerateFiles(directory, "*.md").OrderBy(Path.GetFileName))
        {
            var markdown = File.ReadAllText(file);
            var title = ParseTitle(markdown, Path.GetFileNameWithoutExtension(file));
            var facts = ParseFacts(markdown);
            var cleaned = markdown.Replace("<div class=\"page\"/>", string.Empty, StringComparison.OrdinalIgnoreCase);

            recipes.Add(new Recipe
            {
                Title = title,
                Slug = Slugify(title),
                FileName = Path.GetFileName(file),
                Html = Markdown.ToHtml(RemoveHeaderBlock(cleaned, title), pipeline),
                SearchText = cleaned,
                Facts = facts,
            });
        }

        return recipes;
    }

    private static string RemoveHeaderBlock(string markdown, string title)
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

    private static string ParseTitle(string markdown, string fallback)
    {
        var firstLine = markdown.Split('\n').FirstOrDefault(l => l.StartsWith("# "));
        return firstLine?[2..].Trim() ?? fallback;
    }

    private static Dictionary<string, string> ParseFacts(string markdown)
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

    private static string Slugify(string title)
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
}