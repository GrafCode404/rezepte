using RezepteWeb.Components;
using RezepteWeb.Services;

namespace RezepteWeb;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents();

        var recipesPath = Path.Combine(
            builder.Environment.ContentRootPath,
            builder.Configuration["Recipes:Path"] ?? "Content/Recipes");
        builder.Services.AddSingleton(new RecipeService(recipesPath));

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>();

        app.Run();
    }
}
