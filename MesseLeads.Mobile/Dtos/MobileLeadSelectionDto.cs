namespace MesseLeads.Mobile.Dtos;

public sealed class MobileLeadSelectionDto
{
    public string Group { get; set; } = "";
    public string Key { get; set; } = "";
    public string? Value { get; set; }
    public bool Print { get; set; }
    public bool Digital { get; set; }
}