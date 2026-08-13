namespace MesseLeads.Mobile.Models;

public sealed class LookupGroupSummary
{
    public string Group { get; set; } = "";

    public int Count { get; set; }

    public string DisplayText => $"{Group}: {Count}";
}