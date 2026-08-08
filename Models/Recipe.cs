namespace RezepteWeb.Models;

public class Recipe
{
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required string FileName { get; init; }
    public required string Html { get; init; }
    public required string SearchText { get; init; }
    public Dictionary<string, string> Facts { get; init; } = [];
}