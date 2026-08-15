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

        public FakeHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }

    private static RecipeService CreateService()
    {
        var payload = JsonSerializer.Serialize(new[] { new { name = "Hefezopf.md", content = SampleMarkdown } });
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
        Assert.DoesNotContain("page", recipe.Html, StringComparison.OrdinalIgnoreCase);
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
}
