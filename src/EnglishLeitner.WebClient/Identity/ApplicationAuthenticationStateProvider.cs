using EnglishLeitner.WebClient.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace EnglishLeitner.WebClient.Identity;

public class ApplicationAuthenticationStateProvider : AuthenticationStateProvider
{
    internal const string GoogleAPIsClient = "GoogleAPIs";
    private const string AccessTokenKey = "token";
    private const string DriveAboutAPIEndPoint = "https://www.googleapis.com/drive/v3/about?fields=kind,user,storageQuota";

    private static readonly ClaimsPrincipal _anonymousUser = new(new ClaimsIdentity());
    private static readonly AuthenticationState _anonymousState = new(_anonymousUser);
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILocalStorageService _localStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly System.Timers.Timer _timer = new() { AutoReset = false };

    public ClaimsPrincipal User { get; private set; } = _anonymousUser;

    public ApplicationAuthenticationStateProvider(ILocalStorageService localStorage, IHttpClientFactory httpClientFactory)
    {
        (_localStorage, _httpClientFactory) = (localStorage, httpClientFactory);
        _timer.Elapsed += async (_, _) => await NotifyUserLogoutAsync();
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            TokenInfo? tokenInfo = await GetTokenAsync();
            string? token = tokenInfo?.Token;
            bool isTokenExpired = tokenInfo is null || tokenInfo?.ExpireTime <= DateTime.Now;

            if (string.IsNullOrWhiteSpace(token)
                || isTokenExpired
                || await GetDriveAboutAsync() is not DriveAbout about
                || about?.User is null)
            {
                if (tokenInfo is not null)
                    await RemoveTokenAsync();

                User = _anonymousUser;
                StopWatchingToLogoutOnExpire();
            }
            else
            {
                await StartWatchingToLogoutOnExpireAsync();

                List<Claim> claims = [];

                if (!string.IsNullOrWhiteSpace(about.User.EmailAddress))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, about.User.EmailAddress));
                    claims.Add(new Claim(ClaimTypes.Email, about.User.EmailAddress));
                }

                if (!string.IsNullOrWhiteSpace(about.User.DisplayName))
                    claims.Add(new Claim(ClaimTypes.Name, about.User.DisplayName));

                if (!string.IsNullOrWhiteSpace(about.User.PhotoLink))
                    claims.Add(new Claim("Avatar", about.User.PhotoLink));

                if (!string.IsNullOrWhiteSpace(about.StorageQuota?.Limit))
                    claims.Add(new Claim("StorageQuota.Limit", about.StorageQuota.Limit));

                if (!string.IsNullOrWhiteSpace(about.StorageQuota?.Usage))
                    claims.Add(new Claim("StorageQuota.Usage", about.StorageQuota.Usage));

                if (!string.IsNullOrWhiteSpace(about.StorageQuota?.UsageInDrive))
                    claims.Add(new Claim("StorageQuota.UsageInDrive", about.StorageQuota.UsageInDrive));

                if (!string.IsNullOrWhiteSpace(about.StorageQuota?.UsageInDriveTrash))
                    claims.Add(new Claim("StorageQuota.UsageInDriveTrash", about.StorageQuota.UsageInDriveTrash));

                ClaimsIdentity userIdentity = new(claims, nameof(ApplicationAuthenticationStateProvider));
                User = new ClaimsPrincipal(userIdentity);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            User = _anonymousUser;
            StopWatchingToLogoutOnExpire();
        }

        return User == _anonymousUser
            ? _anonymousState
            : new AuthenticationState(User);
    }

    public async Task NotifyUserLoginAsync(string token, int expiresIn)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);
            await SetTokenAsync(new TokenInfo(token, DateTime.Now.AddSeconds(expiresIn)));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task NotifyUserLogoutAsync()
    {
        try
        {
            await RemoveTokenAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    internal async Task<TokenInfo?> GetTokenAsync()
        => await _localStorage.GetItemAsync<TokenInfo>(AccessTokenKey);

    private async Task<DriveAbout?> GetDriveAboutAsync()
    {
        using HttpClient client = _httpClientFactory.CreateClient(GoogleAPIsClient);

        // docs: https://developers.google.com/workspace/drive/api/reference/rest/v3/about/get
        DriveAbout? result = await client.GetFromJsonAsync<DriveAbout>(
            requestUri: DriveAboutAPIEndPoint,
            options: _serializerOptions);

        return result;
    }

    private async Task SetTokenAsync(TokenInfo value)
        => await _localStorage.SetItemAsync(AccessTokenKey, value);

    private async Task RemoveTokenAsync()
        => await _localStorage.RemoveItemAsync(AccessTokenKey);

    private async Task StartWatchingToLogoutOnExpireAsync()
    {
        TokenInfo? tokenInfo = await GetTokenAsync();
        _timer.Interval = (tokenInfo!.ExpireTime - DateTime.Now).TotalMilliseconds;
        _timer.Start();
    }

    private void StopWatchingToLogoutOnExpire()
         => _timer.Stop();

    internal record TokenInfo(string Token, DateTime ExpireTime);
}