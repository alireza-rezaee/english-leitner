using Microsoft.JSInterop;
using SqliteWasmBlazor;
using static EnglishLeitner.WebClient.Services.DriveSyncService;

namespace EnglishLeitner.WebClient.Pages.Management;

public partial class Data(
    ISqliteWasmDatabaseService dbService,
    ILocalStorageService localStorage)
{
    public const string DbName = "app.db";
    private readonly CancellationTokenSource _disposeCts = new();

    private async Task OnDeleteDatabaseAsync(CancellationToken cancellationToken = default)
    {
        bool isExists = await dbService.ExistsDatabaseAsync(DbName, cancellationToken);
        if (!isExists)
            return;

        await localStorage.SetItemAsync(JsStorageKeyForClearHistoryTime, DateTime.UtcNow);
        await dbService.DeleteDatabaseAsync(DbName, cancellationToken);
    }

    public static string GetRoute()
        => $"/management/data";
}
