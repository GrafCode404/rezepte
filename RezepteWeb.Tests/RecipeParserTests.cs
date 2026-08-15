using RezepteWeb.Services;
using Xunit;

namespace RezepteWeb.Tests;

public class RecipeParserTests
{
    [Theory]
    [InlineData("Hefezopf (Osterzopf)", "hefezopf-osterzopf")]
    [InlineData("Schnelle Grillbrötchen", "schnelle-grillbroetchen")]
    [InlineData("Roggenvollkorn mit Sesam", "roggenvollkorn-mit-sesam")]
    [InlineData("  Rand  Leerzeichen  ", "rand-leerzeichen")]
    [InlineData("ÄÖÜß Test", "aeoeuess-test")]
    public void Slugify_konvertiert_Umlaute_und_Sonderzeichen(string title, string expected)
    {
        Assert.Equal(expected, RecipeParser.Slugify(title));
    }

    [Fact]
    public void Slugify_kollabiert_mehrere_Trennzeichen()
    {
        Assert.Equal("a-b-c", RecipeParser.Slugify("a!!b   c"));
    }

    [Fact]
    public void ParseTitle_nimmt_erste_H1_Zeile()
    {
        var md = "einleitung\n# Mein Rezept\n* **Menge:** 1";
        Assert.Equal("Mein Rezept", RecipeParser.ParseTitle(md, "fallback"));
    }

    [Fact]
    public void ParseTitle_faellt_auf_Fallback_zurueck()
    {
        Assert.Equal("fallback", RecipeParser.ParseTitle("keine überschrift\nnur text", "fallback"));
    }

    [Fact]
    public void ParseFacts_liest_Schluessel_Werte()
    {
        var md = "# T\n* **Menge:** 2 Stück\n* **Backdauer:** 30 Min\nnormaler Text";
        var facts = RecipeParser.ParseFacts(md);

        Assert.Equal(2, facts.Count);
        Assert.Equal("2 Stück", facts["Menge"]);
        Assert.Equal("30 Min", facts["Backdauer"]);
    }

    [Fact]
    public void ExtractIngredients_liest_Zutaten_aus_Tabelle()
    {
        var md = "| Zutaten | 1x | 2x |\n| :--- | :--- | :--- |\n| **Hauptteig** | | |\n| Weizenmehl | 500 g | 1000 g |\n| Butter | 70 g | 140 g |";
        var ingredients = RecipeParser.ExtractIngredients(md);

        Assert.Contains("Weizenmehl", ingredients);
        Assert.Contains("Butter", ingredients);
        Assert.DoesNotContain("500", ingredients);
        // Hinweis: fette Abschnitts-Überschriften ohne Menge werden aktuell mit aufgenommen.
        Assert.Contains("Hauptteig", ingredients);
    }

    [Theory]
    [InlineData("Weizenmehl Type 550", "Weizenmehl", true)]
    [InlineData("Ei", "Ei", true)]
    [InlineData("Weizenmehl", "mehl", false)]
    [InlineData("Sauerteig", "sauer", false)]
    [InlineData("Hefezopf", "zopf", false)]
    public void ContainsWord_prueft_Wortgrenzen(string text, string term, bool expected)
    {
        Assert.Equal(expected, RecipeParser.ContainsWord(text, term));
    }

    [Fact]
    public void RemoveHeaderBlock_entfernt_Titel_und_Fakten()
    {
        var md = "# Mein Rezept\n* **Menge:** 1\n\n## **Anleitungen**\n- Schritt";
        var result = RecipeParser.RemoveHeaderBlock(md, "Mein Rezept");

        Assert.DoesNotContain("# Mein Rezept", result);
        Assert.DoesNotContain("Menge", result);
        Assert.Contains("Anleitungen", result);
    }
}
