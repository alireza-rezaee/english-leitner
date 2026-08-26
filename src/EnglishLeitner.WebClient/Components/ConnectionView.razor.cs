using EnglishLeitner.WebClient.Services;
using Microsoft.AspNetCore.Components;
using static EnglishLeitner.WebClient.Services.InternetConnectivityService;

namespace EnglishLeitner.WebClient.Components;

public partial class ConnectionView(IConnectivityService connectivity)
{
    [Parameter]
    public RenderFragment? Online { get; set; }

    [Parameter]
    public RenderFragment? NoInternet { get; set; }

    [Parameter]
    public RenderFragment? Offline { get; set; }

    private ConnectivityStatus _status = ConnectivityStatus.Connected;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            _status = connectivity.Status;
            connectivity.ConnectionStatusChanged += ConnectionStatusChanged;
        }
    }

    private async void ConnectionStatusChanged(object? sender, ConnectivityStatus status)
    {
        _status = status;
        await InvokeAsync(StateHasChanged);
    }
}
