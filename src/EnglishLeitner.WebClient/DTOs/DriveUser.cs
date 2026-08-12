namespace EnglishLeitner.WebClient.DTOs;

public class DriveUser
{
    public string? Kind { get; set; }
    public string? DisplayName { get; set; }
    public string? PhotoLink { get; set; }
    public bool? Me { get; set; }
    public string? PermissionId { get; set; }
    public string? EmailAddress { get; set; }
}