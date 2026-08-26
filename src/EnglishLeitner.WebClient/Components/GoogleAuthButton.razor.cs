using EnglishLeitner.WebClient.Identity;
using EnglishLeitner.WebClient.Settings;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;
using System.Security.Claims;

namespace EnglishLeitner.WebClient.Components;

public partial class GoogleAuthButton(
    ISnackbar snackbar,
    IJSRuntime jsRuntime,
    IConfiguration configuration,
    ApplicationAuthenticationStateProvider authStateProvider) : IAsyncDisposable
{
    private string _clientId = "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com";
    private string[] _scopes = ["openid"];
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _jsGoogleAuth;
    private DotNetObjectReference<GoogleAuthButton>? _dotnetRef;
    private bool _isSigninPopupOpen = false;

    private bool IsLoggedIn => authStateProvider.User?.Identity?.IsAuthenticated ?? false;
    private string? UserAvatar => authStateProvider.User?.FindFirst("Avatar")?.Value;
    private string? UserName => authStateProvider.User?.Identity?.Name;
    private string? UserEmail => authStateProvider.User?.FindFirst(ClaimTypes.Email)?.Value;
    private bool IsSigninDisabled => IsLoggedIn || _isSigninPopupOpen;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            GoogleSettings googleSettings = configuration
                .GetRequiredSection("Google").Get<GoogleSettings>()!;

            _clientId = googleSettings.ClientId;
            _scopes = googleSettings.Scopes;

            _dotnetRef = DotNetObjectReference.Create(this);

            _jsModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./scripts/js/app.bundle.js");

            string scopes = string.Join(" ", _scopes);
            string? existsToken = (await authStateProvider.GetTokenAsync())?.Token;
            _jsGoogleAuth = await _jsModule.InvokeConstructorAsync(
                "GoogleAuth", _clientId, scopes, _dotnetRef, existsToken);

            await _jsGoogleAuth.InvokeVoidAsync("loadGSILibraryAsync");

            await _jsGoogleAuth.InvokeVoidAsync("initTokenClient");

            authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

            await InvokeAsync(StateHasChanged);
        }
    }

    private async void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        AuthenticationState authState = await task;

        Severity severity;
        string message;

        bool isLoggedIn = authStateProvider.User?.Identity?.IsAuthenticated ?? false;
        if (isLoggedIn)
        {
            string name = authStateProvider.User!.Identity!.Name!;
            (severity, message) = (Severity.Success, $"Hello {name}, welcome!");
        }
        else
        {
            (severity, message) = (Severity.Error, "You are now logged out.");
        }

        snackbar.Add(message, severity);

        await InvokeAsync(StateHasChanged);
    }

    private async Task LoginAsync()
    {
        if (_jsGoogleAuth is not null)
        {
            await _jsGoogleAuth.InvokeVoidAsync("authorize");
            _isSigninPopupOpen = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LogoutAsync()
    {
        if (_jsGoogleAuth is not null)
            await _jsGoogleAuth.InvokeVoidAsync("revoke");
    }

    [JSInvokable]
    public async Task OnJSUserLoginAsync(string token, int expiresIn)
        => await authStateProvider.NotifyUserLoginAsync(token, expiresIn);

    [JSInvokable]
    public async Task OnJSUserLogoutAsync()
        => await authStateProvider.NotifyUserLogoutAsync();

    [JSInvokable]
    public async Task OnJSGooglePopupClosedAsync()
    {
        _isSigninPopupOpen = false;
        await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsGoogleAuth is not null)
            await _jsGoogleAuth.DisposeAsync();

        if (_jsModule is not null)
            await _jsModule.DisposeAsync();

        authStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

        _dotnetRef?.Dispose();
    }
}
