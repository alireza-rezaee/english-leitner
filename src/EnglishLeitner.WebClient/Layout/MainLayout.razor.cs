using EnglishLeitner.WebClient.Services;
using Microsoft.JSInterop;
using MudBlazor;
using static EnglishLeitner.WebClient.Services.InternetConnectivityService;

namespace EnglishLeitner.WebClient.Layout;

public partial class MainLayout(
    ILocalStorageService localStorage,
    IConnectivityService connectivity,
    ISnackbar snackbar) : IAsyncDisposable
{
    private bool _isDarkMode = true;
    private MudTheme? _theme;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _isDarkMode = await localStorage.GetItemAsync<bool>("isDarkMode");

        _theme = new()
        {
            PaletteLight = _lightPalette,
            PaletteDark = _darkPalette,
            LayoutProperties = new LayoutProperties()
        };

        connectivity.ConnectionStatusChanged += ConnectionStatusChanged;
    }

    private async Task DarkModeToggleAsync()
    {
        _isDarkMode = !_isDarkMode;
        await localStorage.SetItemAsync("isDarkMode", _isDarkMode);
    }

    private async void ConnectionStatusChanged(object? sender, ConnectivityStatus status)
    {
        Severity severity;
        string message;

        if (status == ConnectivityStatus.Connected)
            (severity, message) = (Severity.Success, "You're back online.");
        else if (status == ConnectivityStatus.Offline)
            (severity, message) = (Severity.Error, "Network connection lost.");
        else
            (severity, message) = (Severity.Warning, "Internet connection lost.");

        snackbar.Add(message, severity);

        await InvokeAsync(StateHasChanged);
    }

    private readonly PaletteLight _lightPalette = new()
    {
        Black = "#110e2d",
        AppbarText = "#424242",
        AppbarBackground = "rgba(255,255,255,0.8)",
        DrawerBackground = "#ffffff",
        GrayLight = "#e8e8e8",
        GrayLighter = "#f9f9f9",
    };

    private readonly PaletteDark _darkPalette = new()
    {
        Primary = "#7e6fff",
        Surface = "#1e1e2d",
        Background = "#1a1a27",
        BackgroundGray = "#151521",
        AppbarText = "#92929f",
        AppbarBackground = "rgba(26,26,39,0.8)",
        DrawerBackground = "#1a1a27",
        ActionDefault = "#74718e",
        ActionDisabled = "#9999994d",
        ActionDisabledBackground = "#605f6d4d",
        TextPrimary = "#b2b0bf",
        TextSecondary = "#92929f",
        TextDisabled = "#ffffff33",
        DrawerIcon = "#92929f",
        DrawerText = "#92929f",
        GrayLight = "#2a2833",
        GrayLighter = "#1e1e2d",
        Info = "#4a86ff",
        Success = "#3dcb6c",
        Warning = "#ffb545",
        Error = "#ff3f5f",
        LinesDefault = "#33323e",
        TableLines = "#33323e",
        Divider = "#292838",
        OverlayLight = "#1e1e2d80",
    };

    public string DarkLightModeButtonIcon => _isDarkMode switch
    {
        true => Icons.Material.Outlined.LightMode,
        false => Icons.Material.Outlined.DarkMode,
    };

    public async ValueTask DisposeAsync()
    {
        connectivity.ConnectionStatusChanged -= ConnectionStatusChanged;
    }
}
