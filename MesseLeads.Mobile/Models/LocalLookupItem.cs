using SQLite;

namespace MesseLeads.Mobile.Models;

public sealed class LocalLookupItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string? TradeFairKey { get; set; }

    [Indexed]
    public string Group { get; set; } = "";

    public string Key { get; set; } = "";

    public string Label { get; set; } = "";

    public int SortOrder { get; set; }

    public string? Description { get; set; }

    public int FieldType { get; set; }

    public string? OptionA { get; set; }

    public string? OptionB { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}