namespace MesseLeads.Mobile.Models;

public sealed class LeadWizardStep
{
    public required LeadWizardStepKind Kind { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? GroupKey { get; init; }
}