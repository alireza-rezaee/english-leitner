using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.WebClient.Pages;
using EnglishLeitner.WebClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace EnglishLeitner.WebClient.Components;

public partial class AvailableWordsAlert(
    NavigationManager Nav,
    ISyncService SyncService,
    IDbContextFactory<ApplicationDbContext> DbFactory) : IDisposable
{
    private bool _isLoading = true;
    private int _availableCards = default;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await LoadAsync();
            SyncService.OnSyncSucceeded += HandleSyncSucceeded;
        }
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        await InvokeAsync(StateHasChanged);

        await using ApplicationDbContext dbContext = await DbFactory.CreateDbContextAsync();

        DateTime utcNow = DateTime.UtcNow;
        _availableCards = await dbContext.Words
            .IgnoreAutoIncludes()
            .CountAsync(x => x.NextTryUTC == null || x.NextTryUTC <= DateTime.UtcNow);

        _isLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private void NavigateToRandomWord()
    {
        string url = RandomWord.GetRoute();
        Nav.NavigateTo(url, replace: false);
    }

    private Task HandleSyncSucceeded()
        => LoadAsync();

    public void Dispose()
    {
        SyncService.OnSyncSucceeded -= HandleSyncSucceeded;
    }
}
