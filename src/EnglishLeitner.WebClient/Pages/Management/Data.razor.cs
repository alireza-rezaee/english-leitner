using System.Data.Common;
using System.Net;
using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.WebClient.Components;
using EnglishLeitner.WebClient.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using SqliteWasmBlazor;
using static EnglishLeitner.WebClient.Services.DriveSyncService;

namespace EnglishLeitner.WebClient.Pages.Management;

public partial class Data(
    ISnackbar snackbar,
    NavigationManager nav,
    IConfiguration configuration,
    IDialogService dialogService,
    ILocalStorageService localStorage,
    ISqliteWasmDatabaseService dbService,
    IHttpClientFactory httpClientFactory,
    IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const string DbName = "app.db";
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _isLoading = true;
    private bool _isDbInit = false;
    private bool _isDbDownloading = false;
    private double _dbDownloadProgress = 0;
    private string? _dbDownloadStatusMessage;

    [Parameter]
    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public bool? ReturnAfterDownload { get; set; }

    public static async Task<bool> IsDatabaseInitializedAsync(
        ISqliteWasmDatabaseService dbService,
        IDbContextFactory<ApplicationDbContext> dbFactory,
        CancellationToken cancellationToken = default)
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

    public static string GetRoute(string? returnUrl = null)
        => string.IsNullOrWhiteSpace(returnUrl)
            ? $"/management/data"
            : $"/management/data?returnUrl={WebUtility.UrlEncode(returnUrl)}&returnAfterDownload=true";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await LoadAsync(_disposeCts.Token);

        if (ReturnAfterDownload == true
            && !string.IsNullOrWhiteSpace(ReturnUrl)
            && Uri.TryCreate(ReturnUrl, UriKind.RelativeOrAbsolute, out Uri? returnUrl))
        {
            await OnDownloadDatabaseClick();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _isLoading = true;
            await InvokeAsync(StateHasChanged);

            _isDbInit = await IsDatabaseInitializedAsync(dbService, dbFactory, cancellationToken);
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnDownloadDatabaseAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && _isDbInit)
            return;

        _isDbInit = false;
        await InvokeAsync(StateHasChanged);

        await DownloadDatabaseAsync(force, cancellationToken);

        _isDbInit = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnDeleteDatabaseAsync(CancellationToken cancellationToken = default)
    {
        bool isExists = await dbService.ExistsDatabaseAsync(DbName, cancellationToken);
        if (!isExists)
            return;

        await localStorage.SetItemAsync(JsStorageKeyForClearHistoryTime, DateTime.UtcNow);
        await dbService.DeleteDatabaseAsync(DbName, cancellationToken);

        _isDbInit = false;
        await InvokeAsync(StateHasChanged);
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

    public async Task OpenDialogAsync(string title, RenderFragment content, string icon, Color color, Func<Task> onSumbit)
    {
        DialogParameters<GeneralDialog> parameters = new() {
            { x => x.Title, title },
            { x => x.Content, content },
            { x => x.Icon, icon },
            { x => x.Color, color },
        };

        DialogOptions options = new()
        {
            CloseOnEscapeKey = true,
            CloseButton = true,
        };

        IDialogReference dialog = await dialogService.ShowAsync<GeneralDialog>("Delete Server", parameters, options);

        DialogResult? result = await dialog.Result;
        if (result?.Canceled != true)
            await InvokeAsync(onSumbit);
    }

    private async Task OnDownloadDatabaseClick()
    {
        await OpenDialogAsync(
            title: "Download Database",
            content: DownloadDatabaseContent,
            icon: Icons.Material.Outlined.DownloadForOffline,
            color: Color.Info,
            onSumbit: async () =>
            {
                await OnDownloadDatabaseAsync(force: false, _disposeCts.Token);

                if (ReturnAfterDownload == true
                    && !string.IsNullOrWhiteSpace(ReturnUrl)
                    && Uri.TryCreate(ReturnUrl, UriKind.RelativeOrAbsolute, out _))
                {
                    nav.NavigateTo(ReturnUrl);
                }
            });
    }

    private Task OnResetDatabaseClick()
        => OpenDialogAsync(
            title: "Reset Database",
            content: ResetDatabaseContent,
            icon: Icons.Material.Outlined.SettingsBackupRestore,
            color: Color.Error,
            onSumbit: async () => await OnDownloadDatabaseAsync(force: true, _disposeCts.Token)
        );

    private Task OnDeleteDatabaseClick()
        => OpenDialogAsync(
            title: "Delete Database",
            content: DeleteDatabaseContent,
            icon: Icons.Material.Outlined.DeleteForever,
            color: Color.Error,
            onSumbit: async () => await OnDeleteDatabaseAsync(_disposeCts.Token)
        );
}
