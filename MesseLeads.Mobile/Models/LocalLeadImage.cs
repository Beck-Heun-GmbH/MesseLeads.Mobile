using SQLite;

namespace MesseLeads.Mobile.Models;

public sealed class LocalLeadImage
{
    [PrimaryKey]
    public Guid LocalImageId { get; set; } = Guid.NewGuid();

    [Indexed]
    public Guid LeadLocalId { get; set; }

    public int? ServerImageId { get; set; }

    public string LocalFilePath { get; set; } = "";

    public string ImageType { get; set; } = "BusinessCard";

    public string? OcrRawText { get; set; }

    public string? OcrParsedJson { get; set; }

    public string SyncStatus { get; set; } = "Pending";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}