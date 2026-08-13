using System.Text.RegularExpressions;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class HeuristicBusinessCardService
{
    public BusinessCardAiResult Read(string? ocrText)
    {
        var text = ocrText ?? "";
        var result = new BusinessCardAiResult { RawText = text, Engine = "Lokale Regeln" };

        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Regex.Replace(x.Trim(), @"\s+", " "))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        result.Email = Match(text, @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}");
        result.Website = ExtractWebsite(text, result.Email);
        result.Mobile = ExtractPhone(lines, true);
        result.Phone = ExtractPhone(lines, false);
        result.Company = lines.FirstOrDefault(IsCompanyLine);
        result.JobTitle = lines.FirstOrDefault(IsJobTitleLine);
        ExtractAddress(lines, result);
        ExtractName(lines, result);
        result.Country ??= "Deutschland";

        return result;
    }

    private static void ExtractName(IReadOnlyList<string> lines, BusinessCardAiResult result)
    {
        var candidate = lines.FirstOrDefault(line =>
            line != result.Company &&
            line != result.JobTitle &&
            !line.Contains('@') &&
            !line.Any(char.IsDigit) &&
            !IsAddressOrContactLabel(line) &&
            line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is >= 2 and <= 4);

        if (string.IsNullOrWhiteSpace(candidate)) return;
        var parts = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        result.FirstName = parts[0];
        result.LastName = string.Join(" ", parts.Skip(1));
    }

    private static void ExtractAddress(IReadOnlyList<string> lines, BusinessCardAiResult result)
    {
        result.Street = lines.FirstOrDefault(x => Regex.IsMatch(x, @"\b(?:str(?:a(?:ss|ß)e)?|weg|allee|platz|road|street|avenue|lane)\b", RegexOptions.IgnoreCase) && Regex.IsMatch(x, @"\d"));

        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"\b(?:[A-Z]{1,3}-)?(?<zip>\d{4,6})\s+(?<city>[\p{L}][\p{L}\s.\-']+)$", RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            result.ZipCode = match.Groups["zip"].Value;
            result.City = match.Groups["city"].Value.Trim();
            break;
        }
    }

    private static string? ExtractWebsite(string text, string? email)
    {
        foreach (Match match in Regex.Matches(text, @"(?:https?://)?(?:www\.)?[A-Z0-9\-]+(?:\.[A-Z0-9\-]+)+(?::\d+)?(?:/[^\s]*)?", RegexOptions.IgnoreCase))
        {
            if (!match.Value.Contains('@') && !string.Equals(match.Value, email, StringComparison.OrdinalIgnoreCase))
                return match.Value.TrimEnd('.', ',', ';');
        }
        return null;
    }

    private static string? ExtractPhone(IEnumerable<string> lines, bool mobile)
    {
        foreach (var line in lines)
        {
            if (line.Contains("fax", StringComparison.OrdinalIgnoreCase)) continue;
            var isMobile = Regex.IsMatch(line, @"(?:mobil|mobile|cell|\+49\s*\(?0?\)?\s*1[5-7]|\b01[5-7])", RegexOptions.IgnoreCase);
            if (mobile != isMobile) continue;
            var match = Regex.Match(line, @"(?:\+\d{1,3}|0)[\d\s()/\-.]{6,}\d");
            if (match.Success) return Regex.Replace(match.Value.Trim(), @"\s+", " ");
        }
        return null;
    }

    private static bool IsCompanyLine(string line) => Regex.IsMatch(line, @"\b(GmbH|AG|KG|UG|OHG|GbR|SE|Ltd\.?|LLC|Inc\.?|Corp\.?|S\.A\.?|B\.V\.?)\b", RegexOptions.IgnoreCase);
    private static bool IsJobTitleLine(string line) => Regex.IsMatch(line, @"\b(Manager|Leiter|Leitung|Vertrieb|Sales|Director|Geschäftsführer|Berater|Consultant|Engineer|Administrator|Regionalverkaufsleiter)\b", RegexOptions.IgnoreCase);
    private static bool IsAddressOrContactLabel(string line) => Regex.IsMatch(line, @"\b(Tel|Telefon|Phone|Mobil|Mobile|Fax|E-Mail|Mail|Web|www|Straße|Strasse|Street)\b", RegexOptions.IgnoreCase);
    private static string? Match(string text, string pattern) { var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase); return m.Success ? m.Value : null; }
}
