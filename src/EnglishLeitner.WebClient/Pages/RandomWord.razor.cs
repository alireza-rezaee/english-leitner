using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.EFDesign.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace EnglishLeitner.WebClient.Pages;

public partial class RandomWord(IDbContextFactory<ApplicationDbContext> dbFactory, NavigationManager nav)
{
    public static string GetRoute()
        => "/words/random";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            string? nextWordSlug = await GetRandomWordSlugAsync();
            NavigateToCardId(nextWordSlug);
        }
    }

    private void NavigateToCardId(string? slug)
    {
        string url = string.IsNullOrEmpty(slug)
            ? Home.GetRoute()
            : Word.GetRoute(slug);

        nav.NavigateTo(url, replace: true);
    }

    private async Task<string?> GetRandomWordSlugAsync(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext? dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);

        DateTime timeNow = DateTime.UtcNow;
        string? randomWordSlug = await dbContext.Words
            .Where(x => x.NextTryUTC == null || x.NextTryUTC <= timeNow)
            .Select(x => new { x.Slug, x.LeitnerLevel, x.Cefr })
            .OrderBy(x => x.LeitnerLevel)
            .ThenBy(x => x.Cefr.HasValue ? x.Cefr : Cefr.A1)
            .ThenBy(x => EF.Functions.Random())
            .Select(x => x.Slug)
            .FirstOrDefaultAsync(cancellationToken);

        return randomWordSlug;
    }
}
