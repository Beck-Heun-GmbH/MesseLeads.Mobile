namespace MesseLeads.Mobile.Models;

public sealed class QrContactData
{
    public string RawContent { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Company { get; init; }
    public string? JobTitle { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public string? Website { get; init; }
    public string? Street { get; init; }
    public string? ZipCode { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }

    public bool HasContactData =>
        !string.IsNullOrWhiteSpace(FirstName) ||
        !string.IsNullOrWhiteSpace(LastName) ||
        !string.IsNullOrWhiteSpace(Company) ||
        !string.IsNullOrWhiteSpace(JobTitle) ||
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(Phone) ||
        !string.IsNullOrWhiteSpace(Mobile) ||
        !string.IsNullOrWhiteSpace(Website) ||
        !string.IsNullOrWhiteSpace(Street) ||
        !string.IsNullOrWhiteSpace(ZipCode) ||
        !string.IsNullOrWhiteSpace(City) ||
        !string.IsNullOrWhiteSpace(Country);
}
