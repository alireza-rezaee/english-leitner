using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EnglishLeitner.WebClient.Services;

public class JwtAuthenticationStateProvider(ILocalStorageService localStorage) : AuthenticationStateProvider
{
    public ClaimsPrincipal? User { get; set; }

    private static readonly ClaimsPrincipal _anonymousUser = new(new ClaimsIdentity());
    private static readonly AuthenticationState _anonymousState = new(_anonymousUser);

    private const string AuthTokenName = "authToken";

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string? token = await localStorage.GetItemAsync<string>(AuthTokenName);

        if (string.IsNullOrWhiteSpace(token))
        {
            User = null;
            return _anonymousState;
        }

        User = ExtractUserFromToken(token);

        return new AuthenticationState(User);
    }

    public async Task NotifyUserAuthenticationAsync(string token)
    {
        await localStorage.SetItemAsync(AuthTokenName, token);
        User = ExtractUserFromToken(token);
        AuthenticationState authState = new(User);
        NotifyAuthenticationStateChanged(Task.FromResult(authState));
    }

    public async Task NotifyUserLogoutAsync()
    {
        User = _anonymousUser;
        await localStorage.RemoveItemAsync(AuthTokenName);
        NotifyAuthenticationStateChanged(Task.FromResult(_anonymousState));
    }

    private static ClaimsPrincipal ExtractUserFromToken(string token)
    {
        IEnumerable<Claim>? claims = ParseClaimsFromJwt(token);
        ClaimsIdentity identity = new(claims, "GoogleJwt", "name", "role");
        ClaimsPrincipal user = new(identity);

        return user;
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken token = handler.ReadJwtToken(jwt);
        return token.Claims;
    }
}