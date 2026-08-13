namespace MesseLeads.Mobile.Models;

public sealed class LocalSelection
{
    public string Group { get; set; } = "";

    public string Key { get; set; } = "";

    public string? Value { get; set; }

    public bool Print { get; set; }

    public bool Digital { get; set; }
}