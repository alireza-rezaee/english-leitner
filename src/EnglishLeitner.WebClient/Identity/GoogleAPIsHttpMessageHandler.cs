using System.Net.Http.Headers;

namespace EnglishLeitner.WebClient.Identity;

public class GoogleAPIsHttpMessageHandler(ApplicationAuthenticationStateProvider authStateProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? token = (await authStateProvider.GetTokenAsync())?.Token;

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization ??= new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
