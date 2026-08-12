using System.Text.Json.Serialization;

namespace EnglishLeitner.WebClient.DTOs;

public class DriveAbout
{
    public string? Kind { get; set; }
    public DriveUser? User { get; set; }
    public StorageQuota? StorageQuota { get; set; }
}