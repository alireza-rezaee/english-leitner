using Microsoft.JSInterop;

namespace EnglishLeitner.WebClient.Services;

public class InternetConnectivityService : IConnectivityService, IAsyncDisposable
{
    private static readonly int CheckInterval = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;
    private readonly DotNetObjectReference<InternetConnectivityService> _dotnetRef;
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _jsBundle;
    private IJSObjectReference? _jsModule;

    public event EventHandler<ConnectivityStatus>? ConnectionStatusChanged;
    public ConnectivityStatus Status { get; private set; } = ConnectivityStatus.Connected;

    public InternetConnectivityService(IJSRuntime runtime)
    {
        _jsRuntime = runtime;
        _dotnetRef = DotNetObjectReference.Create(this);

        Task.Run(async () =>
        {
            _jsBundle = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./scripts/js/app.bundle.js");

            _jsModule = await _jsBundle.InvokeConstructorAsync(
                "ConnectivityService",
                _dotnetRef,
                CheckInterval);

            var statusNum = await _jsModule.InvokeAsync<int>("checkStatusAsync");
            OnJSConnectionStatusChanged((ConnectivityStatus)statusNum);

            await _jsModule.InvokeVoidAsync("listenAsync");
        });
    }

    [JSInvokable]
    public void OnJSConnectionStatusChanged(ConnectivityStatus status)
    {
        bool isChanged = Status != status;
        if (isChanged)
        {
            Status = status;
            ConnectionStatusChanged?.Invoke(this, status);
        }
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

    public enum ConnectivityStatus
    {
        Offline = 0,
        NoInternetNetwork = 1,
        Connected = 2,
    }
}
