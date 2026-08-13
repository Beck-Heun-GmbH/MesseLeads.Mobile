using CommunityToolkit.Mvvm.ComponentModel;

namespace MesseLeads.Mobile.Models;

public partial class SelectableLookupItem : ObservableObject
{
    public string Group { get; set; } = "";

    public string Key { get; set; } = "";

    public string Label { get; set; } = "";

    /// <summary>0 = Auswahl, 1 = Entweder-oder, 2 = Textfeld.</summary>
    public int FieldType { get; set; }

    public string? OptionA { get; set; }

    public string? OptionB { get; set; }

    /// <summary>Auswahl-Eintrag im Materialbereich mit Print/Digital.</summary>
    public bool UsesPrintDigital { get; set; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool print;

    [ObservableProperty]
    private bool digital;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OptionAChosen))]
    [NotifyPropertyChangedFor(nameof(OptionBChosen))]
    private string? selectedOption;

    [ObservableProperty]
    private string? textValue;

    // Rendering-Steuerung je Feldtyp
    public bool ShowSimpleSelection => FieldType == 0 && !UsesPrintDigital;
    public bool ShowPrintDigital => FieldType == 0 && UsesPrintDigital;
    public bool ShowEitherOr => FieldType == 1;
    public bool ShowText => FieldType == 2;

    public bool OptionAChosen => !string.IsNullOrEmpty(SelectedOption) && SelectedOption == OptionA;
    public bool OptionBChosen => !string.IsNullOrEmpty(SelectedOption) && SelectedOption == OptionB;
}
