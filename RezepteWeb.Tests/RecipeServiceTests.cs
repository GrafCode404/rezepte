using System.Net;
using System.Text;
using System.Text.Json;
using RezepteWeb.Services;
using Xunit;

namespace RezepteWeb.Tests;

public class RecipeServiceTests
{
    private static readonly string SampleMarkdown =
        "# Hefezopf (Osterzopf)\n\n" +
        "* **Menge:** 1 großer oder 2 kleine\n" +
        "* **Backdauer:** 30 - 40 Minuten\n" +
        "\n" +
        "| Zutaten | 1x | 2x |\n" +
        "| :--- | :--- | :--- |\n" +
        "| **Hauptteig** | | |\n" +
        "| Weizenmehl Type 550 | 500 g | 1000 g |\n" +
        "| Butter | 70 g | 140 g |\n" +
        "\n" +
        "<div class=\"page\"/>\n" +
        "\n" +
        "## **Anleitungen**\n\n" +
        "### **Formen**\n" +
        "- Teig teilen.";

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _json;
        private readonly string _contentType;

        public FakeHandler(string json, string contentType = "application/json")
        {
            _json = json;
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, _contentType)
            });
        }
    }

    [Fact]
    public async Task GetRecipesAsync_funktioniert_bei_text_plain_ContentType()
    {
        var payload = JsonSerializer.Serialize(new[] { new { name = "Hefezopf.md", content = SampleMarkdown } });
        var service = new RecipeService(new HttpClient(new FakeHandler(payload, "text/plain")));

        var recipes = await service.GetRecipesAsync();
        Assert.Single(recipes);
    }

    private sealed class FallbackHandler : HttpMessageHandler
    {
        private readonly string _json;

        public FallbackHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host.Contains("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("simulierter Netzwerkfehler");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task GetRecipesAsync_faellt_auf_jsdelivr_zurueck()
    {
        var payload = JsonSerializer.Serialize(new[] { new { name = "Hefezopf.md", content = SampleMarkdown } });
        var service = new RecipeService(new HttpClient(new FallbackHandler(payload)));

        var recipes = await service.GetRecipesAsync();

        Assert.Single(recipes);
        Assert.Equal("Hefezopf (Osterzopf)", recipes[0].Title);
    }

    [Fact]
    public async Task GetRecipesAsync_bei_totalem_Netzwerkfehler_liefert_leere_Liste()
    {
        var service = new RecipeService(new HttpClient(new AlwaysThrowingHandler()));

        var recipes = await service.GetRecipesAsync();
        Assert.Empty(recipes);
    }

    private sealed class AlwaysThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Netzwerk weg");
        }
    }

    private sealed class ApiHandler : HttpMessageHandler
    {
        private readonly string _json;

        public ApiHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host.Contains("api.github.com", StringComparison.OrdinalIgnoreCase))
            {
                var payload = JsonSerializer.Serialize(new
                {
                    name = "index.json",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes(_json)),
                    encoding = "base64"
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
            }
            throw new HttpRequestException("nur Contents API erlaubt");
        }
    }

    [Fact]
    public async Task GetRecipesAsync_nutzt_Contents_API_mit_base64()
    {
        var payload = JsonSerializer.Serialize(new[] { new { name = "Hefezopf.md", content = SampleMarkdown } });
        var service = new RecipeService(new HttpClient(new ApiHandler(payload)));

        var recipes = await service.GetRecipesAsync();

        Assert.Single(recipes);
        Assert.Equal("Hefezopf (Osterzopf)", recipes[0].Title);
        Assert.Equal(SampleMarkdown, recipes[0].Markdown);
    }

    private static RecipeService CreateService()
    {
        var payload = JsonSerializer.Serialize(new[] { new { name = "Hefezopf.md", content = SampleMarkdown } });
        return new RecipeService(new HttpClient(new FakeHandler(payload)));
    }

    private static RecipeService CreateService(params (string Name, string Content)[] entries)
    {
        var payload = JsonSerializer.Serialize(entries.Select(e => new { name = e.Name, content = e.Content }));
        return new RecipeService(new HttpClient(new FakeHandler(payload)));
    }

    [Fact]
    public async Task GetRecipesAsync_parst_Rezept_vollstaendig()
    {
        var service = CreateService();
        var recipes = await service.GetRecipesAsync();

        var recipe = Assert.Single(recipes);
        Assert.Equal("Hefezopf (Osterzopf)", recipe.Title);
        Assert.Equal("hefezopf-osterzopf", recipe.Slug);
        Assert.Equal("Hefezopf.md", recipe.FileName);
        Assert.Equal(SampleMarkdown, recipe.Markdown);
        Assert.Equal("1 großer oder 2 kleine", recipe.Facts["Menge"]);
        Assert.Contains("Weizenmehl Type 550", recipe.Ingredients);
        Assert.Contains("Butter", recipe.Ingredients);
    }

    [Fact]
    public async Task GetRecipesAsync_entfernt_Page_Marker_und_Header_im_Html()
    {
        var service = CreateService();
        var recipes = await service.GetRecipesAsync();

        var recipe = Assert.Single(recipes);
        Assert.DoesNotContain("<div class=\"page\"/>", recipe.Html);
        Assert.Contains("page-break", recipe.Html);
        Assert.DoesNotContain("Hefezopf (Osterzopf)", recipe.Html);
        Assert.Contains("Anleitungen", recipe.Html);
    }

    [Fact]
    public async Task GetBySlugAsync_findet_Rezept_case_insensitive()
    {
        var service = CreateService();

        Assert.NotNull(await service.GetBySlugAsync("hefezopf-osterzopf"));
        Assert.NotNull(await service.GetBySlugAsync("HEFEZOPF-OSTERZOPF"));
        Assert.Null(await service.GetBySlugAsync("gibt-es-nicht"));
    }

    [Fact]
    public async Task SearchAsync_findet_Treffer()
    {
        var service = CreateService();
        await service.GetRecipesAsync();

        var hits = await service.SearchAsync("weizenmehl");
        Assert.Single(hits);

        Assert.Empty(await service.SearchAsync("unbekannt"));
    }

    [Fact]
    public async Task Reset_laedt_erneut()
    {
        var service = CreateService();
        var first = await service.GetRecipesAsync();
        Assert.Single(first);

        service.Reset();
        var second = await service.GetRecipesAsync();
        Assert.Single(second);
    }

    [Fact]
    public async Task GetRecipesAsync_sortiert_nach_Dateinamen()
    {
        var service = CreateService(
            ("Z.md", "# Z\n\n| Zutaten | 1x |\n| :--- | :--- |\n| Zutat | 1 |"),
            ("A.md", "# A\n\n| Zutaten | 1x |\n| :--- | :--- |\n| Apfel | 1 |"));

        var recipes = await service.GetRecipesAsync();

        Assert.Equal(2, recipes.Count);
        Assert.Equal("A", recipes[0].Title);
        Assert.Equal("Z", recipes[1].Title);
    }

    [Fact]
    public async Task SearchAsync_direkte_Treffer_vor_Teilwort_Treffern()
    {
        var service = CreateService(
            ("Teig.md", "# Sauerteig\n\n| Zutaten | 1x |\n| :--- | :--- |\n| Mehl | 1 |"),
            ("Brot.md", "# Brot mit Teig\n\n| Zutaten | 1x |\n| :--- | :--- |\n| Teigling | 1 |"));

        await service.GetRecipesAsync();
        var hits = await service.SearchAsync("teig");

        Assert.Equal(2, hits.Count);
        Assert.Equal("Brot mit Teig", hits[0].Title);
        Assert.Equal("Sauerteig", hits[1].Title);
    }

    [Fact]
    public async Task SearchAsync_leere_Abfrage_liefert_alle()
    {
        var service = CreateService();
        var all = await service.SearchAsync(null);
        Assert.Single(all);

        var whitespace = await service.SearchAsync("   ");
        Assert.Single(whitespace);
    }

    [Fact]
    public async Task GetBySlugAsync_mit_Umlaut_Slug()
    {
        var service = CreateService(
            ("Brot.md", "# Grillbrötchen\n\n* **Menge:** 4"));

        await service.GetRecipesAsync();
        Assert.NotNull(await service.GetBySlugAsync("grillbroetchen"));
        Assert.Null(await service.GetBySlugAsync("grillbrötchen"));
    }

    [Fact]
    public async Task GetRecipesAsync_filtert_geloeschte_aus()
    {
        var deletedMd = "---\ndeleted: true\n---\n# Geloescht\n\n| Zutaten | 1x |\n| :--- | :--- |\n| X | 1 |";
        var activeMd = "# Aktiv\n\n| Zutaten | 1x |\n| :--- | :--- |\n| Y | 1 |";
        var service = CreateService(("Aktiv.md", activeMd), ("Geloescht.md", deletedMd));

        var recipes = await service.GetRecipesAsync();

        Assert.Single(recipes);
        Assert.Equal("Aktiv", recipes[0].Title);
    }

    [Fact]
    public async Task GetDeletedRecipesAsync_liefert_geloeschte()
    {
        var deletedMd = "---\ndeleted: true\n---\n# Geloescht";
        var activeMd = "# Aktiv";
        var service = CreateService(("Aktiv.md", activeMd), ("Geloescht.md", deletedMd));

        var deleted = await service.GetDeletedRecipesAsync();

        Assert.Single(deleted);
        Assert.Equal("Geloescht", deleted[0].Title);
        Assert.StartsWith("---\ndeleted: true", deleted[0].Markdown);
    }

    [Fact]
    public async Task GetBySlugAsync_findet_geloeschte_nicht()
    {
        var deletedMd = "---\ndeleted: true\n---\n# Geloescht";
        var service = CreateService(("Geloescht.md", deletedMd));

        Assert.Null(await service.GetBySlugAsync("geloescht"));
    }

    [Fact]
    public async Task SearchAsync_ignoriert_geloeschte()
    {
        var deletedMd = "---\ndeleted: true\n---\n# Sauerteig Spezial\n\n| Zutaten | 1x |\n| :--- | :--- |\n| Mehl | 1 |";
        var activeMd = "# Sauerteig\n\n| Zutaten | 1x |\n| :--- | :--- |\n| Mehl | 1 |";
        var service = CreateService(("Aktiv.md", activeMd), ("Geloescht.md", deletedMd));

        var hits = await service.SearchAsync("sauerteig");

        Assert.Single(hits);
        Assert.Equal("Sauerteig", hits[0].Title);
    }
}
