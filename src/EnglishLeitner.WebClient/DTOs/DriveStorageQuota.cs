using System.Text.Json.Serialization;

namespace EnglishLeitner.WebClient.DTOs;

public class StorageQuota
{
    public string? Limit { get; set; }
    public string? Usage { get; set; }
    public string? UsageInDrive { get; set; }
    public string? UsageInDriveTrash { get; set; }
}