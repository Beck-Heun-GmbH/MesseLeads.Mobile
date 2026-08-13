namespace MesseLeads.Mobile.Services;

public sealed class QwenBusinessCardPromptBuilder
{
    public string Build(string ocrText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ocrText);

        return $$"""
<|im_start|>system
Du extrahierst Kontaktdaten aus OCR-Texten von Visitenkarten.
Antworte ausschliesslich mit genau einem gueltigen JSON-Objekt.
Erfinde keine Werte. Nicht vorhandene oder unsichere Werte muessen null sein.
Trenne Vorname und Nachname.
Entferne Titel wie Dr., Prof. oder Dipl.-Ing. aus firstName/lastName.
Waehle als Person bevorzugt eine eigene Namenszeile, besonders mit Titel wie Dr. oder Prof.
Namen in Firmenzeilen wie "Firma - Inhabername" gehoeren zur company, nicht automatisch zur Kontaktperson.
Company darf ueber mehrere Zeilen gehen. Kombiniere Firmenname, Inhaber-/Namenszusatz und Rechtsform.
Ordne Tel., Telefon und Phone zu phone.
Ordne nur Mobil, Mobile, Cell oder Handy zu mobile.
Faxnummern duerfen nie in phone oder mobile uebernommen werden.
Eine Regionsbezeichnung ist keine Adresse.
Verwende exakt diese JSON-Felder:
{
  "firstName": null,
  "lastName": null,
  "company": null,
  "jobTitle": null,
  "email": null,
  "phone": null,
  "mobile": null,
  "website": null,
  "street": null,
  "zipCode": null,
  "city": null,
  "country": null
}
<|im_end|>
<|im_start|>user
OCR-TEXT:
{{ocrText.Trim()}}
<|im_end|>
<|im_start|>assistant
""";
    }
}
