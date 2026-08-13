namespace MesseLeads.Mobile.Dtos;

public sealed class MobileLookupItemDto
{
    public string? TradeFairKey { get; set; }

    public string Group { get; set; } = "";

    public string Key { get; set; } = "";

    public string Label { get; set; } = "";

    public int SortOrder { get; set; }

    public string? Description { get; set; }

    public int FieldType { get; set; }

    public string? OptionA { get; set; }

    public string? OptionB { get; set; }
}