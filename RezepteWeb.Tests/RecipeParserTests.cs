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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! --- ###")]
    public void Slugify_leer_bei_keinen_Zeichen(string input)
    {
        Assert.Equal("", RecipeParser.Slugify(input));
    }

    [Fact]
    public void Slugify_ist_kleinbuchstaben()
    {
        Assert.Equal("bagels", RecipeParser.Slugify("BAGELS"));
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
    public void ParseTitle_ignoriert_untergeordnete_Überschriften()
    {
        Assert.Equal("fallback", RecipeParser.ParseTitle("## Level 2\n### Level 3", "fallback"));
    }

    [Fact]
    public void ParseTitle_erkennt_nur_H1_mit_Leerzeichen()
    {
        Assert.Equal("fallback", RecipeParser.ParseTitle("#KeinLeerzeichen", "fallback"));
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
    public void ParseFacts_ignoriert_malformierte_Zeilen()
    {
        var md = "# T\n* **KeinDoppelpunkt**\n* normaler Text\n* **Gueltig:** 1";
        var facts = RecipeParser.ParseFacts(md);

        Assert.Single(facts);
        Assert.Equal("1", facts["Gueltig"]);
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

    [Fact]
    public void ExtractIngredients_unterstuetzt_CRLF_Zeilenumbrueche()
    {
        var md = "| Zutaten | 1x |\r\n| :--- | :--- |\r\n| Mehl | 500 g |";
        var ingredients = RecipeParser.ExtractIngredients(md);

        Assert.Contains("Mehl", ingredients);
    }

    [Fact]
    public void ExtractIngredients_stoppt_nach_der_Tabelle()
    {
        var md = "| Zutaten | 1x |\n| :--- | :--- |\n| Mehl | 500 g |\n\n## **Anleitungen**\n- Schritt";
        var ingredients = RecipeParser.ExtractIngredients(md);

        Assert.Equal("Mehl", ingredients);
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

    [Fact]
    public void ParseFrontmatter_liest_deleted_Flag()
    {
        Assert.True(RecipeParser.IsDeleted("---\ndeleted: true\n---\n# Titel"));
        Assert.True(RecipeParser.IsDeleted("---\ndeleted: 1\n---\n# Titel"));
        Assert.False(RecipeParser.IsDeleted("---\ndeleted: false\n---\n# Titel"));
    }

    [Fact]
    public void IsDeleted_false_ohne_Frontmatter()
    {
        Assert.False(RecipeParser.IsDeleted("# Titel\nText"));
    }

    [Fact]
    public void MarkDeleted_stellt_Frontmatter_voran()
    {
        var marked = RecipeParser.MarkDeleted("# Titel\nText");

        Assert.StartsWith("---\ndeleted: true\n---\n", marked);
        Assert.True(RecipeParser.IsDeleted(marked));
    }

    [Fact]
    public void MarkDeleted_ist_idempotent()
    {
        var marked = RecipeParser.MarkDeleted("# Titel");
        Assert.Equal(marked, RecipeParser.MarkDeleted(marked));
    }

    [Fact]
    public void MarkRestored_entfernt_Frontmatter()
    {
        var restored = RecipeParser.MarkRestored("---\ndeleted: true\n---\n# Titel");

        Assert.Equal("# Titel", restored);
        Assert.False(RecipeParser.IsDeleted(restored));
    }

    [Fact]
    public void StripFrontmatter_laesst_Rezepte_ohne_Frontmatter_unveraendert()
    {
        var md = "# Titel\nText";
        Assert.Equal(md, RecipeParser.StripFrontmatter(md));
    }

    [Fact]
    public void MarkRestored_ohne_Frontmatter_bleibt_unveraendert()
    {
        Assert.Equal("# Titel", RecipeParser.MarkRestored("# Titel"));
    }

    [Fact]
    public void StripFrontmatter_unterstuetzt_CRLF()
    {
        var md = "---\r\ndeleted: true\r\n---\r\n# Titel";
        Assert.Equal("# Titel", RecipeParser.StripFrontmatter(md));
        Assert.True(RecipeParser.IsDeleted(md));
    }
}
