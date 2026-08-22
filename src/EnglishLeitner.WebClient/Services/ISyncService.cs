namespace EnglishLeitner.WebClient.Services;

public interface ISyncService
{
    event Func<Task> OnSyncSucceeded;
    event Func<string, Task> OnSyncFailed;
    
    Task SyncAsync();
}
