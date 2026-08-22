using EnglishLeitner.EFDesign.Data;
using EnglishLeitner.WebClient;
using EnglishLeitner.WebClient.Identity;
using EnglishLeitner.WebClient.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using SqliteWasmBlazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

#if DEBUG
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
#else
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);
#endif

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLocalStorageServices();

builder.Services.AddScoped<IConnectivityService, InternetConnectivityService>();

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

Uri baseAddress = new(builder.HostEnvironment.BaseAddress);
builder.Services.AddHttpClient("Application", (sp, client) =>
{   
    // Why BaseUri from NavigationManager?
    // see: https://stackoverflow.com/a/79320244
    NavigationManager navigationManager = sp.GetRequiredService<NavigationManager>();
    client.BaseAddress = new Uri(navigationManager.BaseUri);
});

builder.Services.AddScoped<GoogleAPIsHttpMessageHandler>();
builder.Services.AddHttpClient(name: ApplicationAuthenticationStateProvider.GoogleAPIsClient,
      client => client.BaseAddress = new Uri("https://www.googleapis.com/"))
    .AddHttpMessageHandler<GoogleAPIsHttpMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("Application"));

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
#if DEBUG
    var connection = new SqliteWasmConnection("Data Source=app.db", LogLevel.Information);
#else
    var connection = new SqliteWasmConnection("Data Source=app.db", LogLevel.Error);
#endif
    options.UseSqliteWasm(connection);
});

builder.Services.AddSqliteWasm();
builder.Services.AddScoped<ISyncService, DriveSyncService>();

var host = builder.Build();
await host.Services.InitializeSqliteWasmAsync();

await host.RunAsync();
