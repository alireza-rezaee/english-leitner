using static EnglishLeitner.WebClient.Services.InternetConnectivityService;

namespace EnglishLeitner.WebClient.Services;

public interface IConnectivityService
{
    event EventHandler<ConnectivityStatus>? ConnectionStatusChanged;
    ConnectivityStatus Status { get; }
}
