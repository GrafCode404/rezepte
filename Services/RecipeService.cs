using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Markdig;
using RezepteWeb.Models;

namespace RezepteWeb.Services;

public class RecipeService
{
    private readonly HttpClient _http;
    private readonly List<Recipe> _recipes = [];
    private Task? _loadTask;

    public RecipeService(HttpClient http)
    {
        _http = http;
    }

    public static MarkdownPipeline CreatePipeline()
        => new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    public async Task<List<Recipe>> GetRecipesAsync()
    {
        await EnsureLoadedAsync();
        return _recipes;
    }

    public async Task<Recipe?> GetBySlugAsync(string slug)
    {
        await EnsureLoadedAsync();
        return _recipes.FirstOrDefault(r => r.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<Recipe>> SearchAsync(string? query)
    {
        await EnsureLoadedAsync();
        if (string.IsNullOrWhiteSpace(query))
        {
            return _recipes;
        }

        var needle = query.Trim();
        return _recipes.Where(r =>
            r.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            r.SearchText.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task LoadAsync()
    {
        var entries = await _http.GetFromJsonAsync<RecipeEntry[]>("recipes/index.json") ?? [];
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        foreach (var entry in entries.OrderBy(e => e.Name))
        {
            var title = ParseTitle(entry.Content, Path.GetFileNameWithoutExtension(entry.Name));
            var facts = ParseFacts(entry.Content);
            var cleaned = entry.Content.Replace("<div class=\"page\"/>", string.Empty, StringComparison.OrdinalIgnoreCase);

            _recipes.Add(new Recipe
            {
                Title = title,
                Slug = Slugify(title),
                FileName = entry.Name,
                Html = Markdown.ToHtml(RemoveHeaderBlock(cleaned, title), pipeline),
                SearchText = cleaned,
                Facts = facts,
            });
        }
    }

    private static string ParseTitle(string markdown, string fallback)
    {
        var firstLine = markdown.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("# "));
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

    private class RecipeEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }
}