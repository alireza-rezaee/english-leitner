using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.EFDesign.Models;
using EnglishLeitner.WebClient.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using static EnglishLeitner.WebClient.Identity.ApplicationAuthenticationStateProvider;
using WordModel = EnglishLeitner.EFDesign.Models.Word;

namespace EnglishLeitner.WebClient.Services;

public class DriveSyncService : ISyncService, IAsyncDisposable
{
    public const string JsStorageKeyForClearHistoryTime = "clearHistoryAt";
    const string JsStorageKeyForDataCheckSum = "dataCheckSum";
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ILocalStorageService _localStorage;
    private readonly ApplicationAuthenticationStateProvider _authStateProvider;
    private readonly DotNetObjectReference<DriveSyncService> _dotnetRef;
    private IJSObjectReference? _jsBundle;

    private IJSObjectReference? _jsModule;

    public event Func<Task>? OnSyncSucceeded;
    public event Func<string, Task>? OnSyncFailed;

    public DriveSyncService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ILocalStorageService localStorage,
        ApplicationAuthenticationStateProvider authStateProvider,
        IJSRuntime jsRuntime)
    {
        _dbFactory = dbFactory;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _dotnetRef = DotNetObjectReference.Create(this);

        Task.Run(async () =>
        {
            _jsBundle = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./scripts/js/app.bundle.js");

            _jsModule = await _jsBundle.InvokeConstructorAsync(
                "DriveSyncService", _dotnetRef);
        });
    }

    public async Task SyncAsync()
    {
        await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync();
        List<Review> local = await dbContext.Reviews.ToListAsync();

        string localChecksum = GetSHA256(ReviewsToJsonString(local));
        string? liveChecksum = await GetRemoteCheckSumAsync();
        if (localChecksum == liveChecksum)
            return;

        Review[] remote = await GetRemoteDataAsync();

        IEnumerable<Review> merged = Enumerable.Concat(local, remote);
        DateTime? clearHistoryTime = await _localStorage.GetItemAsync<DateTime>(JsStorageKeyForClearHistoryTime);
        if (clearHistoryTime is not null)
        {
            IEnumerable<Review> toRemove = merged.Where(x => x.Time < clearHistoryTime);
            merged = merged.Except(toRemove);
        }

        var (words, reviews) = ExtractWordAndReviews(merged);

        if (remote.ContainsAnyExcept(reviews) || reviews.ContainsAnyExcept(remote))
        {
            string? md5CheckSum = await SetRemoteDataAsync(reviews);
            if (!string.IsNullOrWhiteSpace(md5CheckSum))
                await _localStorage.SetItemAsync<string>(JsStorageKeyForDataCheckSum, md5CheckSum);
        }

        bool isAnyLocalChange = false;
        IEnumerable<Review> toDrop = local.Except(reviews);
        IEnumerable<Review> toInsert = reviews.Except(local);

        if (toDrop.Any())
        {
            dbContext.RemoveRange(toDrop);
            isAnyLocalChange = true;
        }

        if (toInsert.Any())
        {
            await dbContext.AddRangeAsync(toInsert);
            isAnyLocalChange = true;
        }

        int[] wordIds = [.. words.Select(x => x.Id)];
        List<WordModel> toUpdateWords = await dbContext.Words.Where(x => wordIds.Contains(x.Id)).ToListAsync();

        var toUpdatePairs = toUpdateWords
            .Join(words, n => n.Id, o => o.Id, (n, o) => (ToUpdate: n, Goal: o))
            .Where(x =>
                x.ToUpdate.NextTryUTC != x.Goal.NextTryUTC ||
                x.ToUpdate.LeitnerLevel != x.Goal.LeitnerLevel)
            .ToArray();

        if (toUpdatePairs.Length > 0)
        {
            isAnyLocalChange = true;
            foreach (var (toUpdate, goal) in toUpdatePairs)
            {
                toUpdate.NextTryUTC = goal.NextTryUTC;
                toUpdate.LeitnerLevel = goal.LeitnerLevel;
            }
            dbContext.Words.UpdateRange(toUpdateWords);
        }

        if (isAnyLocalChange)
            await dbContext.SaveChangesAsync();

        string? lastSyncChecksum = await _localStorage.GetItemAsync<string>(JsStorageKeyForDataCheckSum);
        bool isFirstPull = string.IsNullOrWhiteSpace(lastSyncChecksum);
        if (isFirstPull && !string.IsNullOrWhiteSpace(liveChecksum))
            await _localStorage.SetItemAsync<string>(JsStorageKeyForDataCheckSum, liveChecksum);

        if (OnSyncSucceeded is not null)
                await OnSyncSucceeded.Invoke();
    }

    [JSInvokable]
    public async Task OnJSSyncErrorAsync(string error)
    {
        if (OnSyncFailed is not null)
                await OnSyncFailed.Invoke(error);
    }

    private async Task<string?> GetRemoteCheckSumAsync()
    {
        TokenInfo? tokenInfo = await _authStateProvider.GetTokenAsync();
        if (_jsModule is null || string.IsNullOrWhiteSpace(tokenInfo?.Token))
            return null;

        return await _jsModule.InvokeAsync<string>("getRemoteCheckSumAsync", tokenInfo.Token);
    }

    private async Task<Review[]> GetRemoteDataAsync()
    {
        TokenInfo? tokenInfo = await _authStateProvider.GetTokenAsync();
        if (_jsModule is null || string.IsNullOrWhiteSpace(tokenInfo?.Token))
            return [];

        string? json = await _jsModule.InvokeAsync<string?>("getRemoteDataAsync", tokenInfo.Token);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<Review[]>(json) ?? [];
    }

    private async Task<string?> SetRemoteDataAsync(IEnumerable<Review> reviews)
    {
        TokenInfo? tokenInfo = await _authStateProvider.GetTokenAsync();
        if (_jsModule is null || string.IsNullOrWhiteSpace(tokenInfo?.Token))
            return null;

        string json = ReviewsToJsonString(reviews);
        return await _jsModule.InvokeAsync<string>("setRemoteDataAsync", json, tokenInfo.Token);
    }

    private static (WordModel[] Words, Review[] Reviews) ExtractWordAndReviews(IEnumerable<Review> reviews)
    {
        const int MaxLevel = 5;
        List<WordModel> words = [];
        List<Review> result = [.. reviews];

        List<IGrouping<int, Review>> groups = [.. result.GroupBy(r => r.WordId)];
        foreach (var group in groups)
        {
            var ordered = group.OrderBy(x => x.Time).ToArray();

            int leitnerLevel = 0;
            DateTime? nextTryTime = null;
            foreach (var review in ordered)
            {
                bool isOK = nextTryTime is null || nextTryTime < review.Time;
                if (isOK)
                {
                    if (review.IsRemembered)
                        leitnerLevel = leitnerLevel < MaxLevel ? leitnerLevel + 1 : MaxLevel;
                    else
                        leitnerLevel = 0;

                    int nextDays = (int)Math.Pow(2, leitnerLevel);
                    nextTryTime = review.Time.AddDays(nextDays);
                    continue;
                }

                result.Remove(review);
            }

            words.Add(new WordModel
            {
                Id = group.Key,
                LeitnerLevel = leitnerLevel,
                NextTryUTC = nextTryTime,
            });
        }

        return (words.ToArray(), result.ToArray());
    }

    private static string ReviewsToJsonString(IEnumerable<Review> reviews)
    {
        List<Review> ordered = reviews.OrderBy(x => x.Time).ToList();
        return JsonSerializer.Serialize(ordered);
    }

    private static string GetSHA256(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("dispose");
            await _jsModule.DisposeAsync();
        }

        if (_jsBundle is not null)
            await _jsBundle.DisposeAsync();

        _dotnetRef?.Dispose();
    }
}
