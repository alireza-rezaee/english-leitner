using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.WebClient.Components;
using EnglishLeitner.WebClient.DTOs;
using EnglishLeitner.WebClient.Pages.Management;
using EnglishLeitner.WebClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using SqliteWasmBlazor;

namespace EnglishLeitner.WebClient.Pages;

public partial class Home(
    IJSRuntime jsRuntime,
    NavigationManager nav,
    ISyncService syncService,
    IDialogService dialogService,
    ISqliteWasmDatabaseService dbService,
    IDbContextFactory<ApplicationDbContext> dbFactory) : IDisposable
{
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _isLoading = true;
    private DateOnlyRange _dateRange = null!;
    public Dictionary<DateOnly, int>? _model;

    private ICollection<ReviewCalendarItem>? ReviewItems
    {
        get => _model?
            .Select(x => (Date: x.Key, ReviewsCount: x.Value))
            .Select(x => new ReviewCalendarItem()
            {
                Date = x.Date,
                CssClass = "mud-theme-info" + x.ReviewsCount switch
                {
                    > 75 => string.Empty,
                    > 50 => " opacity-75",
                    > 25 => " opacity-50",
                    > 0 => " opacity-25",
                    _ => string.Empty
                },
                Tooltip = $"{x.ReviewsCount} {(x.ReviewsCount > 1 ? "Reviews" : "Review")} on {x.Date:M}",
            }).ToList();
    }

    private string ReviewCalendarTitle => _model?.Sum(x => x.Value) switch
    {
        > 1 => $"{_model?.Sum(x => x.Value)} Reviews",
        1 => "1 Review",
        0 or _ => "No Review",
    } + " in the last year";

    public static string GetRoute()
        => $"/";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender)
            return;

        syncService.OnSyncSucceeded += HandleSyncSucceeded;
        await LoadAsync(_disposeCts.Token);
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _isLoading = true;
            await InvokeAsync(StateHasChanged);

            _dateRange = new DateOnlyRange(
                Start: DateOnly.FromDateTime(DateTime.Now.Date.AddYears(-1).AddDays(1)),
                End: DateOnly.FromDateTime(DateTime.Now.Date));

            DateTime startTime = _dateRange.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            DateTime endTime = _dateRange.End.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local).ToUniversalTime();

            bool isDbInit = await Data.IsDatabaseInitializedAsync(dbService, dbFactory, cancellationToken);
            if (!isDbInit)
            {
                await OpenWelcomeDialogAsync(cancellationToken);
                return;
            }

            await using ApplicationDbContext dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);
            List<DateTime> reviewTimes = await dbContext.Reviews
                .Where(x => x.Time >= startTime && x.Time < endTime)
                .Select(x => x.Time)
                .ToListAsync(cancellationToken);

            _model = reviewTimes
                .GroupBy(utcTime =>
                    DateOnly.FromDateTime(
                        DateTime.SpecifyKind(utcTime, DateTimeKind.Utc).ToLocalTime()))
                .ToDictionary(g => g.Key, g => g.Count());
        }
        finally
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OpenWelcomeDialogAsync(CancellationToken cancellationToken = default)
    {
        DialogParameters<GeneralDialog> parameters = new() {
            { x => x.Title, "Welcome" },
            { x => x.Content, WelcomeDialogContent },
            { x => x.Icon, Icons.Material.Outlined.WavingHand },
            { x => x.Color, Color.Info },
        };

        DialogOptions options = new()
        {
            CloseOnEscapeKey = false,
            CloseButton = false,
        };

        IDialogReference dialog = await dialogService.ShowAsync<GeneralDialog>("Delete Server", parameters, options);

        DialogResult? result = await dialog.Result;
        if (result?.Canceled != true)
            nav.NavigateTo(Data.GetRoute(nav.Uri));
        else
            await GoBackAsync();

    }

    private async Task HandleSyncSucceeded()
    {
        await InvokeAsync(async () =>
        {
            await LoadAsync(_disposeCts.Token);
        });
    }

    private async Task GoBackAsync()
        => await jsRuntime.InvokeVoidAsync("history.back");

    public void Dispose()
    {
        syncService.OnSyncSucceeded -= HandleSyncSucceeded;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
