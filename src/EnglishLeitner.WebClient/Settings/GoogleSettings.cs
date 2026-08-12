namespace EnglishLeitner.WebClient.Settings;

public class GoogleSettings
{
    public string ClientId { get; set; } = "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com";
    public string[] Scopes { get; set; } = ["openid"];
}
