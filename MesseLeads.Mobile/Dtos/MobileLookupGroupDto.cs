namespace MesseLeads.Mobile.Dtos;

public sealed class MobileLookupGroupDto
{
    public string? TradeFairKey { get; set; }

    public string Key { get; set; } = "";

    public string Label { get; set; } = "";

    public int SortOrder { get; set; }

    public string? Description { get; set; }
}
