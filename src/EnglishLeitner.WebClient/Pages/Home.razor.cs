using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.WebClient.DTOs;
using EnglishLeitner.WebClient.Services;
using EnglishLeitner.WebClient.Settings;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using SqliteWasmBlazor;
using System.Data.Common;
using static EnglishLeitner.WebClient.Services.DriveSyncService;

namespace EnglishLeitner.WebClient.Pages;

public partial class Home(
    ISnackbar snackbar,
    ISyncService syncService,
    IConfiguration configuration,
    ILocalStorageService localStorage,
    IHttpClientFactory httpClientFactory,
    ISqliteWasmDatabaseService dbService,
    IDbContextFactory<ApplicationDbContext> dbFactory) : IDisposable
{
    const string DbName = "app.db";
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _isLoading = true;
    private bool _isDbImported = true;
    private bool _isDbDownloading = false;
    private double _dbDownloadProgress = 0;
    private string? _dbDownloadStatusMessage;
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

            _isDbImported = await IsDatabaseReadyAsync(cancellationToken);
            await InvokeAsync(StateHasChanged);

            if (!_isDbImported)
            {
                await DownloadDatabaseAsync(force: true, cancellationToken);
                _isDbImported = true;
                await InvokeAsync(StateHasChanged);
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

    private async Task<bool> IsDatabaseReadyAsync(CancellationToken cancellationToken = default)
    {
        bool isDbExist = await dbService.ExistsDatabaseAsync(DbName, cancellationToken);
        Console.WriteLine($"IsDatabaseReadyAsync > isDbExist? {(isDbExist ? "true" : "false")}");
        if (!isDbExist)
            return false;

        // Word table is exists?
        // It may be removed by clearing the browser data.
        await using ApplicationDbContext dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using DbConnection dbConnection = dbContext.Database.GetDbConnection();
        await using DbCommand command = dbConnection.CreateCommand();

        command.CommandText = $$"""
            SELECT EXISTS (
                SELECT 1
                FROM sqlite_master
                WHERE type = 'table' AND name = 'Words'
            );
            """;

        await dbConnection.OpenAsync(cancellationToken);
        bool isTableExists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;

        return isTableExists;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0";

        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];

        var unitIndex = (int)Math.Floor(Math.Log(bytes, 1024));
        var value = bytes / Math.Pow(1024, unitIndex);

        return $"{value:0.##} {units[unitIndex]}";
    }

    private async Task OnDeleteDatabaseAsync(CancellationToken cancellationToken = default)
    {
        bool isExists = await dbService.ExistsDatabaseAsync(DbName, cancellationToken);
        if (!isExists)
            return;

        await localStorage.SetItemAsync(JsStorageKeyForClearHistoryTime, DateTime.UtcNow);
        await dbService.DeleteDatabaseAsync(DbName, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    private async Task DownloadDatabaseAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        _isDbDownloading = true;
        _dbDownloadProgress = 0;
        _dbDownloadStatusMessage = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            bool isExists = await dbService.ExistsDatabaseAsync(DbName, cancellationToken);

            if (isExists && !force)
                return;

            ApplicationSettings appSettings = configuration
                .GetRequiredSection("Application").Get<ApplicationSettings>()!;

            await using MemoryStream stream = new();
            Progress<(long BytesRead, long? TotalBytes)>? progressReporter = new(async info =>
            {
                _dbDownloadProgress = info.TotalBytes > 0
                    ? info.BytesRead * 100d / info.TotalBytes.Value
                    : 0; // todo: handle unknown total bytes case

                _dbDownloadStatusMessage = info.TotalBytes.HasValue
                    ? $"({FormatBytes(info.BytesRead)}/{FormatBytes((long)info.TotalBytes)})"
                    : "downloading...";

                await InvokeAsync(StateHasChanged);
            });

            await DownloadToStreamAsync(stream, appSettings.DatabaseUrl, progressReporter, cancellationToken);

            if (isExists)
                await dbService.DeleteDatabaseAsync(DbName, cancellationToken);

            stream.Position = 0;
            await dbService.ImportDatabaseFromStreamAsync(DbName, stream, stream.Length, null, cancellationToken);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(ex.Message))
                snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isDbDownloading = false;
            _dbDownloadProgress = 0;
            _dbDownloadStatusMessage = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task DownloadToStreamAsync(
        Stream targetStream,
        string url,
        IProgress<(long BytesRead, long? TotalBytes)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using HttpClient httpClient = httpClientFactory.CreateClient("Application");
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.SetBrowserResponseStreamingEnabled(true);

        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        if (totalBytes.HasValue)
            targetStream.SetLength((long)totalBytes);

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        byte[] buffer = new byte[64 * 1024]; // 64 KiB
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            progress?.Report((totalBytesRead, totalBytes));
        }
    }

    private async Task HandleSyncSucceeded()
    {
        await InvokeAsync(async () =>
        {
            await LoadAsync(_disposeCts.Token);
        });
    }

    public void Dispose()
    {
        syncService.OnSyncSucceeded -= HandleSyncSucceeded;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
