namespace MesseLeads.Mobile.Dtos;

public sealed class MobileLeadUploadRequest
{
    public Guid MobileLocalId { get; set; }
    public string DeviceId { get; set; } = "";

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

    public List<MobileLeadSelectionDto> Selections { get; set; } = [];
}
