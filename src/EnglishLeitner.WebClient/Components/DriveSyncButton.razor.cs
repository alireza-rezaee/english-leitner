using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.WebClient.Identity;
using EnglishLeitner.WebClient.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;

namespace EnglishLeitner.WebClient.Components;

public partial class DriveSyncButton(ISnackbar snackbar, ISyncService syncService) : IDisposable
{
    private SyncStatus _syncStatus = SyncStatus.Normal;

    private string SyncIcon => _syncStatus switch
    {
        SyncStatus.InProcess => Icons.Material.Outlined.CloudSync,
        SyncStatus.Succeded => Icons.Material.Outlined.CloudDone,
        SyncStatus.Failed => Icons.Material.Outlined.SyncProblem,
        SyncStatus.Normal or _ => Icons.Material.Outlined.Cloud,
    };

    private Color SyncColor => _syncStatus switch
    {
        SyncStatus.InProcess => Color.Primary,
        SyncStatus.Succeded => Color.Success,
        SyncStatus.Failed => Color.Error,
        SyncStatus.Normal or _ => Color.Default,
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            syncService.OnSyncSucceeded += HandleSyncSucceeded;
            syncService.OnSyncFailed += HandleSyncFailed;
        }
    }

    private async Task SaveAsync()
    {
        _syncStatus = SyncStatus.InProcess;
        await InvokeAsync(StateHasChanged);

        await syncService.SyncAsync();

        _syncStatus = SyncStatus.Normal;
        await InvokeAsync(StateHasChanged);
    }

    private Task HandleSyncSucceeded()
        => KeepSyncStatusForCertainTime(TimeSpan.FromSeconds(5), SyncStatus.Succeded);

    private Task HandleSyncFailed(string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
            snackbar.Add($"Sync error: {errorMessage}", Severity.Error);

        return KeepSyncStatusForCertainTime(TimeSpan.FromSeconds(5), SyncStatus.Failed);
    }

    private async Task KeepSyncStatusForCertainTime(TimeSpan time, SyncStatus syncStatus)
    {
        _syncStatus = syncStatus;
        await InvokeAsync(StateHasChanged);

        await Task.Delay(time);

        _syncStatus = SyncStatus.Normal;
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        syncService.OnSyncSucceeded -= HandleSyncSucceeded;
        syncService.OnSyncFailed -= HandleSyncFailed;
    }

    private enum SyncStatus
    {
        Normal,
        InProcess,
        Succeded,
        Failed,
    }
}
