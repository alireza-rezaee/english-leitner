using EnglishLeitner.WebClient;
using EnglishLeitner.WebClient.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLocalStorageServices();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ApplicationAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => 
    provider.GetRequiredService<ApplicationAuthenticationStateProvider>());

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.RequireInteraction = false;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
    config.SnackbarConfiguration.HideTransitionDuration = (int)TimeSpan.FromSeconds(0.5).TotalMilliseconds;
    config.SnackbarConfiguration.ShowTransitionDuration = (int)TimeSpan.FromSeconds(0.5).TotalMilliseconds;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<GoogleAPIsHttpMessageHandler>();
builder.Services.AddHttpClient(name: ApplicationAuthenticationStateProvider.GoogleAPIsClient, 
      client => client.BaseAddress = new Uri("https://www.googleapis.com/"))
    .AddHttpMessageHandler<GoogleAPIsHttpMessageHandler>();
    
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
  .CreateClient(name: ApplicationAuthenticationStateProvider.GoogleAPIsClient));

await builder.Build().RunAsync();
