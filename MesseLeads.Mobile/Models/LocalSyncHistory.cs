using SQLite;

namespace MesseLeads.Mobile.Models;

public sealed class LocalSyncHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    public DateTime FinishedUtc { get; set; } = DateTime.UtcNow;

    public bool Success { get; set; }

    public string Trigger { get; set; } = "Manuell";

    public int LeadTotal { get; set; }

    public int LeadSynced { get; set; }

    public int LeadFailed { get; set; }

    public int ImageSynced { get; set; }

    public int ImageFailed { get; set; }

    public string Message { get; set; } = "";
}
