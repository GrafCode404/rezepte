using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Markdig;
using RezepteWeb.Models;

namespace RezepteWeb.Services;

public class RecipeService
{
    public const string RecipesIndexUrl =
        "https://raw.githubusercontent.com/GrafCode404/rezepte-content/main/recipes/index.json";

    public const string RecipesIndexApiUrl =
        "https://api.github.com/repos/GrafCode404/rezepte-content/contents/recipes/index.json";

    private readonly HttpClient _http;
    private readonly List<Recipe> _recipes = [];
    private readonly List<Recipe> _deleted = [];
    private Task? _loadTask;

    public RecipeService(HttpClient http)
    {
        _http = http;
    }

    public static MarkdownPipeline CreatePipeline()
        => new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    public void Reset()
    {
        _recipes.Clear();
        _deleted.Clear();
        _loadTask = null;
    }

    public async Task<List<Recipe>> GetRecipesAsync()
    {
        await EnsureLoadedAsync();
        return _recipes;
    }

    public async Task<List<Recipe>> GetDeletedRecipesAsync()
    {
        await EnsureLoadedAsync();
        return _deleted;
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

        var direct = new List<Recipe>();
        var partial = new List<Recipe>();
        foreach (var recipe in _recipes)
        {
            if (recipe.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                recipe.Ingredients.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                if (RecipeParser.ContainsWord(recipe.Title, needle) || RecipeParser.ContainsWord(recipe.Ingredients, needle))
                {
                    direct.Add(recipe);
                }
                else
                {
                    partial.Add(recipe);
                }
            }
        }

        direct.AddRange(partial);
        return direct;
    }

    private async Task LoadAsync()
    {
        var entries = await FetchEntriesAsync();
        if (entries is null)
        {
            return;
        }

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        foreach (var entry in entries.OrderBy(e => e.Name))
        {
            var content = RecipeParser.StripFrontmatter(entry.Content);
            var title = RecipeParser.ParseTitle(content, Path.GetFileNameWithoutExtension(entry.Name));
            var facts = RecipeParser.ParseFacts(content);
            var cleaned = content.Replace("<div class=\"page\"/>", "<div class=\"page-break\"></div>", StringComparison.OrdinalIgnoreCase);

            var recipe = new Recipe
            {
                Title = title,
                Slug = RecipeParser.Slugify(title),
                FileName = entry.Name,
                Markdown = entry.Content,
                Html = Markdown.ToHtml(RecipeParser.RemoveHeaderBlock(cleaned, title), pipeline),
                Ingredients = RecipeParser.ExtractIngredients(cleaned),
                Facts = facts,
            };

            if (RecipeParser.IsDeleted(entry.Content))
            {
                _deleted.Add(recipe);
            }
            else
            {
                _recipes.Add(recipe);
            }
        }
    }

    private async Task<RecipeEntry[]?> FetchEntriesAsync()
    {
        // 1. GitHub Contents API – sofort aktuell, kein CDN-Cache
        //    (raw.githubusercontent cached index.json 5 Min. und ignoriert ?v=).
        try
        {
            using var response = await _http.GetAsync(RecipesIndexApiUrl);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<GitHubContentResponse>();
                if (body is not null && body.Content.Length > 0)
                {
                    var clean = body.Content.Replace("\n", "").Replace("\r", "");
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(clean));
                    return JsonSerializer.Deserialize<RecipeEntry[]>(json);
                }
            }
        }
        catch (Exception)
        {
            // weiter mit Fallbacks
        }

        // 2. raw.githubusercontent (fallback, kann bis 5 Min. veraltet sein)
        // 3. jsDelivr (fallback, kann bis 12 Std. veraltet sein)
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string[] urls =
        [
            RecipesIndexUrl + "?v=" + timestamp,
            "https://cdn.jsdelivr.net/gh/GrafCode404/rezepte-content@main/recipes/index.json",
        ];

        foreach (var url in urls)
        {
            try
            {
                return await _http.GetFromJsonAsync<RecipeEntry[]>(url);
            }
            catch (Exception)
            {
                // nächste Quelle versuchen
            }
        }

        return null;
    }

    private class GitHubContentResponse
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonPropertyName("encoding")]
        public string Encoding { get; set; } = "";
    }

    private class RecipeEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }
}
