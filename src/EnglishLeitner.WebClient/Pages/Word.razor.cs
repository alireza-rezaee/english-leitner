using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.EFDesign.Models;
using EnglishLeitner.WebClient.DTOs;
using EnglishLeitner.WebClient.Pages.Management;
using EnglishLeitner.WebClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SqliteWasmBlazor;
using WordModel = EnglishLeitner.EFDesign.Models.Word;

namespace EnglishLeitner.WebClient.Pages;

public partial class Word(
    IJSRuntime jsRuntime,
    NavigationManager nav,
    ISyncService syncService,
    ISqliteWasmDatabaseService dbService,
    IDbContextFactory<ApplicationDbContext> dbFactory) : IDisposable
{
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _isLoading = true;
    private DateOnlyRange _dateRange = default!;
    private int _todayReviewsCount = default;
    private string[] _subtitleParts = [];
    private bool _isAnswerVisible = false;
    private IJSObjectReference? _jsBundle;
    private IJSObjectReference? _jsModule;
    private WordModel? _model;

    private List<ReviewCalendarItem>? ReviewItems
    {
        get
        {
            return _model?.Reviews?.Select(x => new ReviewCalendarItem()
            {
                Date = DateOnly.FromDateTime(DateTime.SpecifyKind(x.Time, DateTimeKind.Utc).ToLocalTime()),
                CssClass = x.IsRemembered ? "mud-theme-success" : "mud-theme-error",
                Tooltip = $"{(x.IsRemembered ? "Remembered" : "Forgotten")} on {x.Time:M}",
            }).ToList();
        }
    }

    private string ReviewCalendarTitle => ReviewItems?.Count switch
    {
        > 1 => $"{ReviewItems.Count} Reviews",
        1 => "1 Review",
        0 or _ => "No Review",
    } + " in the last year";

    private bool IsAnswerDisabled => _model?.NextTryUTC > DateTime.UtcNow;

    [Parameter]
    public required string Slug { get; set; }

    public static string GetRoute(string slug)
        => $"/words/{slug}";

    protected override Task OnParametersSetAsync()
        => LoadWordAsync(_disposeCts.Token);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await LoadWordAsync(_disposeCts.Token);
            syncService.OnSyncSucceeded += HandleSyncSucceeded;
        }
    }

    private async Task LoadWordAsync(CancellationToken cancellationToken = default)
    {
        _isLoading = true;
        await InvokeAsync(StateHasChanged);

        _dateRange = new DateOnlyRange(
            Start: DateOnly.FromDateTime(DateTime.Today.AddYears(-1).AddDays(1)),
            End: DateOnly.FromDateTime(DateTime.Today));

        try
        {
            DateTime startTime = _dateRange.Start.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            DateTime endTime = _dateRange.End.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

            bool isDbInit = await Data.IsDatabaseInitializedAsync(dbService, dbFactory, cancellationToken);
            if (!isDbInit)
            {
                nav.NavigateTo(Data.GetRoute(nav.Uri));
                return;
            }

            await using ApplicationDbContext dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);
            _model = await dbContext.Words
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.Pronunciations)
                .Include(x => x.Meanings)
                    .ThenInclude(x => x.Meanings)
                    .ThenInclude(x => x.Examples)
                .Include(x => x.Reviews.Where(review =>
                    review.Time >= startTime &&
                    review.Time < endTime))
                .FirstOrDefaultAsync(x => x.Slug == Slug, cancellationToken: cancellationToken);

            List<string> subtitleParts = [];
            if (_model?.Cefr is Cefr cefr)
                subtitleParts.Add(cefr.ToString());
            if (!string.IsNullOrWhiteSpace(_model?.Position))
                subtitleParts.Add(_model.Position.ToString());
            if (!string.IsNullOrWhiteSpace(_model?.Grammar))
                subtitleParts.Add(_model.Grammar.ToString());
            _subtitleParts = [.. subtitleParts];

            DateTime todayStartTime = DateTime.Today.ToUniversalTime();
            DateTime tomorrowStartTime = DateTime.Today.AddDays(1).ToUniversalTime();
            _todayReviewsCount = await dbContext.Reviews
                .CountAsync(x =>
                    x.Time >= todayStartTime &&
                    x.Time < tomorrowStartTime, cancellationToken: cancellationToken);

            _jsBundle = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./scripts/js/app.bundle.js");

            _jsModule = await _jsBundle.InvokeConstructorAsync("Word");
        }
        finally
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnForgetAsync(CancellationToken cancellationToken = default)
        => await OnTryAsync(false, cancellationToken);

    private async Task OnRememberAsync(CancellationToken cancellationToken = default)
        => await OnTryAsync(true, cancellationToken);

    private void ToggleAnswerDisplay()
        => _isAnswerVisible = !_isAnswerVisible;

    private async Task PlayAudioAsync(string url, CancellationToken cancellationToken = default)
    {
        if (_jsModule is not null)
            await _jsModule.InvokeVoidAsync("play", cancellationToken: cancellationToken, url);
    }

    private void GoBackHome()
    {
        string url = Home.GetRoute();
        nav.NavigateTo(url, replace: false);
    }

    private void NavigateToRandomCard()
    {
        string url = RandomWord.GetRoute();
        nav.NavigateTo(url, replace: false);
    }

    private async Task OnTryAsync(bool isRemember, CancellationToken cancellationToken = default)
    {
        if (_model is null)
            return;

        Review review = new()
        {
            WordId = _model.Id,
            IsRemembered = isRemember,
            Time = DateTime.UtcNow,
        };

        if (isRemember)
        {
            const int MaxLevel = 5;
            _model.LeitnerLevel = _model.LeitnerLevel < MaxLevel
                ? _model.LeitnerLevel + 1
                : MaxLevel;
        }
        else
            _model.LeitnerLevel = 0;

        int nextDays = (int)Math.Pow(2, _model.LeitnerLevel);
        _model.NextTryUTC = DateTime.UtcNow.AddDays(nextDays);

        await using ApplicationDbContext? dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Reviews.AddAsync(review, cancellationToken);
        dbContext.Words.Update(_model);
        await dbContext.SaveChangesAsync(cancellationToken);

        NavigateToRandomCard();
    }

    private Task HandleSyncSucceeded()
        => LoadWordAsync(_disposeCts.Token);

    public void Dispose()
    {
        syncService.OnSyncSucceeded -= HandleSyncSucceeded;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
