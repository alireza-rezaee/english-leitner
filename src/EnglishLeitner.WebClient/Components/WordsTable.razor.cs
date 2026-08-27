using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.WebClient.DTOs;
using EnglishLeitner.WebClient.Pages;
using EnglishLeitner.WebClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using WordModel = EnglishLeitner.EFDesign.Models.Word;

namespace EnglishLeitner.WebClient.Components;

public partial class WordsTable(
    ISyncService syncService,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    NavigationManager nav) : IDisposable
{
    private const string LastReviewKey = "LastReview";

    private MudTable<WordModel> _table = default!;

    private string _searchString = string.Empty;

    private bool _reviewsOnly = true;

    [Parameter]
    public DateOnlyRange? DateRange { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
            syncService.OnSyncSucceeded += HandleSyncSucceeded;
    }

    private async Task<TableData<WordModel>> ServerReload(TableState state, CancellationToken token)
    {
        await using ApplicationDbContext dbContext = await dbFactory.CreateDbContextAsync();

        IQueryable<WordModel>? query = dbContext.Words
            .Include(w => w.Reviews
                .OrderByDescending(r => r.Time)
                .Take(1));

        if (!string.IsNullOrWhiteSpace(_searchString))
            query = query.Where(x =>
                (x.HeadWord != null && x.HeadWord.Contains(_searchString)) ||
                (x.Position != null && x.Position.Contains(_searchString)) ||
                (x.Grammar != null && x.Grammar.Contains(_searchString)) ||
                (x.Cefr != null && x.Cefr.ToString()!.Contains(_searchString)));

        if (DateRange is not null && _reviewsOnly)
        {
            DateTime startTime = DateRange.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            DateTime endTime = DateRange.End.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(x => x.Reviews.Any(y => y.Time >= startTime && y.Time < endTime));
        }

        int totalItems = await query.CountAsync(cancellationToken: token);

        query = state.SortLabel switch
        {
            nameof(WordModel.Id) => query = query.OrderByDirection(state.SortDirection, x => x.Id),
            nameof(WordModel.HeadWord) => query = query.OrderByDirection(state.SortDirection, x => x.HeadWord),
            nameof(WordModel.Position) => query = query.OrderByDirection(state.SortDirection, x => x.Position),
            nameof(WordModel.Grammar) => query = query.OrderByDirection(state.SortDirection, x => x.Grammar),
            nameof(WordModel.Cefr) => query = query.OrderByDirection(state.SortDirection, x => x.Cefr),
            nameof(WordModel.Reviews) => query = query.OrderByDirection(state.SortDirection, x => x.Reviews.Count),
            nameof(LastReviewKey) => query = query.OrderByDirection(state.SortDirection, x => x.Reviews.Any() ?
x.Reviews.First().Time : DateTime.MinValue),
            nameof(WordModel.LeitnerLevel) => query = query.OrderByDirection(state.SortDirection, x => x.LeitnerLevel),
            _ => query,
        };

        ICollection<WordModel>? pagedData = await query
            .Skip(state.Page * state.PageSize)
            .Take(state.PageSize)
            .ToListAsync();

        return new TableData<WordModel>()
        {
            TotalItems = totalItems,
            Items = pagedData
        };
    }

    private async Task OnSearchAsync(string text)
    {
        _searchString = text;
        await _table.ReloadServerData();
    }

    private async Task OnReviewsOnlyChangedAsync(bool value)
    {
        _reviewsOnly = value;
        await _table.ReloadServerData();
    }

    private void RowClickEvent(TableRowClickEventArgs<WordModel> tableRowClickEventArgs)
    {
        if (tableRowClickEventArgs.Item is WordModel word)
        {
            string url = Word.GetRoute(word.Slug);
            nav.NavigateTo(url, replace: false);
        }
    }

    private Task HandleSyncSucceeded()
        => _table.ReloadServerData();

    public void Dispose()
    {
        syncService.OnSyncSucceeded -= HandleSyncSucceeded;
    }
}
