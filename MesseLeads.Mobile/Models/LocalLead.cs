using SQLite;

namespace MesseLeads.Mobile.Models;

public sealed class LocalLead
{
    [PrimaryKey]
    public Guid LocalId { get; set; } = Guid.NewGuid();

    public int? ServerId { get; set; }

    public string SyncStatus { get; set; } = "Draft";

    public int SyncRetryCount { get; set; }

    public string? LastSyncError { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastSyncedUtc { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Company { get; set; }

    public string? JobTitle { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Mobile { get; set; }

    public string? Street { get; set; }

    public string? ZipCode { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? Website { get; set; }

    public string? TradeFairKey { get; set; }

    public string? TradeFair { get; set; }

    public string? VisitDay { get; set; }

    public string? OwnerUser { get; set; }

    public bool WantsNewsletter { get; set; }

    public string? Notes { get; set; }

    public string? FollowUpNotes { get; set; }

    public string SelectionsJson { get; set; } = "[]";
}
