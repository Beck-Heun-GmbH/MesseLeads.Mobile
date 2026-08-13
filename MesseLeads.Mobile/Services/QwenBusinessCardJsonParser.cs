using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class QwenBusinessCardJsonParser
{
    private static readonly string[] KnownTopLevelDomains =
    [
        "at", "be", "biz", "ch", "co", "com", "de", "dk", "es", "eu",
        "fr", "gr", "info", "io", "it", "net", "nl", "org", "pl", "uk"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public BusinessCardAiResult Parse(string generatedText, string rawOcrText)
    {
        var json = ExtractJsonObject(generatedText);
        return ParseJson(json, rawOcrText);
    }

    public BusinessCardAiResult ParseJson(string json, string rawOcrText)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Die Qwen-Antwort enthält kein auswertbares JSON-Objekt.");
        }

        var payload = JsonSerializer.Deserialize<QwenBusinessCardPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("Die Qwen-Antwort enthält kein auswertbares JSON-Objekt.");

        var result = new BusinessCardAiResult
        {
            FirstName = Clean(payload.FirstName),
            LastName = Clean(payload.LastName),
            Company = Clean(payload.Company),
            JobTitle = Clean(payload.JobTitle),
            Email = Clean(payload.Email),
            Phone = Clean(payload.Phone),
            Mobile = Clean(payload.Mobile),
            Website = Clean(payload.Website),
            Street = Clean(payload.Street),
            ZipCode = Clean(payload.ZipCode),
            City = Clean(payload.City),
            Country = Clean(payload.Country),
            RawText = rawOcrText,
            Engine = "Qwen2.5-1.5B / llama.cpp"
        };

        ApplyOcrCorrections(result, rawOcrText);
        result.ParsedJson = FormatResultJson(result);

        return result;
    }

    public string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Qwen hat keine Antwort geliefert.");
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("Die Qwen-Antwort enthält kein JSON-Objekt.");
        }

        return text[start..(end + 1)];
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch
        {
            return json;
        }
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return string.Equals(cleaned, "null", StringComparison.OrdinalIgnoreCase)
            ? null
            : cleaned;
    }

    private static void ApplyOcrCorrections(
        BusinessCardAiResult result,
        string rawOcrText)
    {
        var lines = NormalizeLines(rawOcrText);
        if (lines.Count == 0)
        {
            return;
        }

        var contactName = ExtractContactName(lines, result);
        if (contactName is not null)
        {
            result.FirstName = contactName.Value.FirstName;
            result.LastName = contactName.Value.LastName;
        }

        var company = ExtractCompany(lines, result);
        if (!string.IsNullOrWhiteSpace(company))
        {
            result.Company = company;
        }

        result.Email = ExtractEmail(rawOcrText) ?? result.Email;
        result.Website = ExtractWebsite(lines, result.Email) ?? result.Website;

        if (!IsValidWebsiteCandidate(result.Website, result.Email))
        {
            result.Website = string.Empty;
        }

        var phone = ExtractPhone(lines, mobile: false);
        var mobile = ExtractPhone(lines, mobile: true);

        result.Phone = phone ?? result.Phone;
        result.Mobile = mobile ?? result.Mobile ?? string.Empty;

        if (IsFaxNumber(result.Phone, lines))
        {
            result.Phone = string.Empty;
        }

        if (IsFaxNumber(result.Mobile, lines))
        {
            result.Mobile = string.Empty;
        }

        if (IsSameNumber(result.Phone, result.Mobile) &&
            LooksLikeMobileNumber(result.Mobile) &&
            !HasPhoneLabelForNumber(result.Phone, lines))
        {
            result.Phone = string.Empty;
        }

        ApplyAddress(lines, result);
    }

    private static List<string> NormalizeLines(string text)
    {
        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Regex.Replace(x.Trim(), @"\s+", " "))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static (string FirstName, string LastName)? ExtractContactName(
        IReadOnlyList<string> lines,
        BusinessCardAiResult current)
    {
        foreach (var line in lines)
        {
            var match = Regex.Match(
                line,
                @"^(?:(?:Dr|Prof|Professor|Dipl\.-Ing|Ing)\.?\s+)+(?<name>[\p{L}][\p{L}\-']+(?:\s+[\p{L}][\p{L}\-']+){1,3})$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                continue;
            }

            return SplitName(match.Groups["name"].Value);
        }

        if (HasPlausibleName(current))
        {
            return null;
        }

        foreach (var line in lines)
        {
            if (IsCompanyLine(line) ||
                IsJobTitleLine(line) ||
                IsAddressOrContactLabel(line) ||
                IsSloganOrMarketingLine(line) ||
                line.Contains('@') ||
                line.Contains('-') ||
                line.Any(char.IsDigit))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is >= 2 and <= 4)
            {
                return SplitName(line);
            }
        }

        return null;
    }

    private static bool HasPlausibleName(BusinessCardAiResult result)
    {
        if (string.IsNullOrWhiteSpace(result.FirstName) ||
            string.IsNullOrWhiteSpace(result.LastName))
        {
            return false;
        }

        var fullName = $"{result.FirstName} {result.LastName}";
        return !IsCompanyLine(fullName) &&
               !IsJobTitleLine(fullName) &&
               !IsSloganOrMarketingLine(fullName) &&
               fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is >= 2 and <= 4;
    }

    private static (string FirstName, string LastName) SplitName(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], string.Join(" ", parts.Skip(1)));
    }

    private static string? ExtractCompany(
        IReadOnlyList<string> lines,
        BusinessCardAiResult current)
    {
        var legalIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (IsCompanyLine(lines[i]) && !StartsWithJobTitle(lines[i]))
            {
                legalIndex = i;
                break;
            }
        }

        if (legalIndex < 0)
        {
            return null;
        }

        var legalLine = lines[legalIndex];
        string extracted;
        if (!IsPureLegalSuffixLine(legalLine))
        {
            extracted = legalLine;
        }
        else
        {
            var prefix = lines
                .Take(legalIndex)
                .Reverse()
                .FirstOrDefault(line =>
                    line.Contains('-') &&
                    IsPotentialCompanyPrefix(line));

            prefix ??= lines
                .Take(legalIndex)
                .Reverse()
                .FirstOrDefault(IsPotentialCompanyPrefix);

            extracted = string.IsNullOrWhiteSpace(prefix)
                ? legalLine
                : $"{prefix} {legalLine}";
        }

        return IsBetterCompany(extracted, current.Company)
            ? extracted
            : null;
    }

    private static bool IsPotentialCompanyPrefix(string line)
    {
        return !IsJobTitleLine(line) &&
               !IsAddressOrContactLabel(line) &&
               !IsSloganOrMarketingLine(line) &&
               !line.Contains('@') &&
               !line.Any(char.IsDigit);
    }

    private static bool IsBetterCompany(string extracted, string? current)
    {
        if (string.IsNullOrWhiteSpace(extracted) ||
            StartsWithJobTitle(extracted) ||
            IsSloganOrMarketingLine(extracted))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        if (!IsCompanyLine(current))
        {
            return true;
        }

        var normalizedExtracted = NormalizeCompanyComparison(extracted);
        var normalizedCurrent = NormalizeCompanyComparison(current);

        return !string.Equals(normalizedExtracted, normalizedCurrent, StringComparison.Ordinal) &&
               extracted.Length >= current.Length - 2;
    }

    private static void ApplyAddress(
        IReadOnlyList<string> lines,
        BusinessCardAiResult result)
    {
        var street = lines.FirstOrDefault(x =>
            Regex.IsMatch(
                x,
                @"\b(?:str(?:a(?:ss|ß)e)?|weg|allee|platz|road|street|avenue|lane)\b",
                RegexOptions.IgnoreCase) &&
            Regex.IsMatch(x, @"\d"));

        if (!string.IsNullOrWhiteSpace(street))
        {
            result.Street = CleanStreet(street);
        }

        foreach (var line in lines)
        {
            var match = Regex.Match(
                line,
                @"\b(?:[A-Z]{1,3}-)?(?<zip>\d{4,6})\s+(?<city>[\p{L}][\p{L}\s.\-']+?)(?:\s*\((?<country>[^)]+)\))?$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                continue;
            }

            result.ZipCode = match.Groups["zip"].Value;
            result.City = match.Groups["city"].Value.Trim();

            if (match.Groups["country"].Success)
            {
                result.Country = match.Groups["country"].Value.Trim();
            }

            break;
        }
    }

    private static string? ExtractEmail(string text)
    {
        var match = Regex.Match(
            text,
            @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Value : null;
    }

    private static string? ExtractWebsite(IEnumerable<string> lines, string? email)
    {
        foreach (var line in lines)
        {
            var searchableLine = Regex.Replace(
                line,
                @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
                " ",
                RegexOptions.IgnoreCase);

            foreach (Match match in Regex.Matches(
                         searchableLine,
                         @"(?:https?://)?(?:www\.)?[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?)+(?::\d+)?(?:/[^\s]*)?",
                         RegexOptions.IgnoreCase))
            {
                var value = CleanWebsite(match.Value);
                if (IsValidWebsiteCandidate(value, email))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? ExtractPhone(
        IEnumerable<string> lines,
        bool mobile)
    {
        foreach (var line in lines)
        {
            if (HasFaxLabel(line))
            {
                continue;
            }

            var labeledMatch = Regex.Match(
                line,
                mobile
                    ? @"(?:\b(?:mobil|mobile|cell|handy|mob)\b\.?|(?:^|\s)M\b)\s*:?\s*(?<number>(?:\+\d{1,3}|0)[\d\s()/\-.]{6,}\d)"
                    : @"\b(?:tel|telefon|phone|fon)\b\.?\s*:?\s*(?<number>(?:\+\d{1,3}|0)[\d\s()/\-.]{6,}\d)",
                RegexOptions.IgnoreCase);

            if (labeledMatch.Success)
            {
                return CleanPhone(labeledMatch.Groups["number"].Value);
            }
        }

        foreach (var line in lines)
        {
            if (HasFaxLabel(line))
            {
                continue;
            }

            var match = Regex.Match(line, @"(?:\+\d{1,3}|0)[\d\s()/\-.]{6,}\d");
            if (!match.Success)
            {
                continue;
            }

            var number = CleanPhone(match.Value);
            var isMobile = HasMobileLabel(line) || LooksLikeMobileNumber(number);
            var isPhone = HasPhoneLabel(line) || (!isMobile && !HasMobileLabel(line));

            if (mobile && isMobile)
            {
                return number;
            }

            if (!mobile && isPhone)
            {
                return number;
            }
        }

        return null;
    }

    private static bool IsFaxNumber(string? number, IEnumerable<string> lines)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return false;
        }

        var normalized = NormalizeNumber(number);
        return lines.Any(line =>
            line.Contains("fax", StringComparison.OrdinalIgnoreCase) &&
            NormalizeNumber(line).Contains(normalized, StringComparison.Ordinal));
    }

    private static string NormalizeNumber(string value)
    {
        return Regex.Replace(value, @"[^\d+]", "");
    }

    private static bool IsCompanyLine(string line)
    {
        return Regex.IsMatch(
            line,
            @"\b(?:GmbH|AG|KG|UG|OHG|GbR|SE|LLC)\b|\b(?:Ltd|Inc|Corp)\.?\b|\bS\.?\s*A\.?\b|\bB\.?\s*V\.?\b",
            RegexOptions.IgnoreCase);
    }

    private static bool IsPureLegalSuffixLine(string line)
    {
        return Regex.IsMatch(
            line,
            @"^(?:GmbH|UG|AG|KG|OHG|GbR|SE|Ltd\.?|LLC|Inc\.?|Corp\.?)(?:\s*&\s*Co\.?)?(?:\s*(?:KG|OHG|GmbH))?\.?$",
            RegexOptions.IgnoreCase);
    }

    private static bool IsJobTitleLine(string line)
    {
        return Regex.IsMatch(
            line,
            @"\b(Manager|Leiter|Leitung|Vertrieb|Sales|Director|Geschäftsführer|Berater|Consultant|Engineer|Administrator|Regionalverkaufsleiter)\b",
            RegexOptions.IgnoreCase);
    }

    private static bool StartsWithJobTitle(string line)
    {
        return Regex.IsMatch(
            line,
            @"^\s*(?:Manager|Leiter|Leitung|Vertrieb|Sales|Director|Geschäftsführer|President|Präsident|Berater|Consultant|Engineer|Administrator|Regionalverkaufsleiter|Fachberater)\b",
            RegexOptions.IgnoreCase);
    }

    private static bool IsAddressOrContactLabel(string line)
    {
        return Regex.IsMatch(
            line,
            @"\b(Tel|Telefon|Phone|Mobil|Mobile|Mob|Fax|E-Mail|Mail|Web|www|Straße|Strasse|Street)\b",
            RegexOptions.IgnoreCase);
    }

    private static bool IsSloganOrMarketingLine(string line)
    {
        return Regex.IsMatch(
            line,
            @"\b(Aluminium|shading|inspire|Software\s+Distribution|Software\s+Solutions|Europaweite|BESTE\s+WERTE|Werte\s+f[uü]rs\s+Haus|Leidenschaft|Zukunft)\b",
            RegexOptions.IgnoreCase);
    }

    private static string NormalizeCompanyComparison(string value)
    {
        return Regex.Replace(value, @"[\s.\-+&]+", "").ToLowerInvariant();
    }

    private static string CleanStreet(string street)
    {
        var cleaned = Regex.Replace(street.Trim(), @"^\d+\s+(?=.*\d)", "");
        var match = Regex.Match(
            cleaned,
            @"^(?<street>.+?\d+[A-Z]?)\s*,?\s+(?:[A-Z]{1,3}-)?\d{4,6}\s+[\p{L}].*$",
            RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups["street"].Value.Trim().TrimEnd(',', ';')
            : cleaned;
    }

    private static string CleanWebsite(string value)
    {
        return value
            .Trim()
            .TrimEnd('.', ',', ';', ':', ')', ']')
            .TrimStart('(', '[');
    }

    private static bool IsValidWebsiteCandidate(string? value, string? email)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains('@') ||
            string.Equals(value, email, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = GetWebsiteHost(value);
        if (string.IsNullOrWhiteSpace(host) ||
            !host.Contains('.') ||
            !HasKnownTopLevelDomain(host))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var localPart = email.Split('@')[0];
            if (string.Equals(host, localPart, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, localPart, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetWebsiteHost(string value)
    {
        var host = Regex.Replace(value.Trim(), @"^https?://", "", RegexOptions.IgnoreCase);
        host = host.Split('/')[0];
        host = host.Split(':')[0];
        return host.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static bool HasKnownTopLevelDomain(string host)
    {
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 &&
               KnownTopLevelDomains.Contains(parts[^1], StringComparer.OrdinalIgnoreCase);
    }

    private static string CleanPhone(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static bool HasFaxLabel(string line)
    {
        return line.Contains("fax", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMobileLabel(string line)
    {
        return Regex.IsMatch(
            line,
            @"\b(?:mobil|mobile|cell|handy|mob)\b\.?|(?:^|\s)M\b",
            RegexOptions.IgnoreCase);
    }

    private static bool HasPhoneLabel(string line)
    {
        return Regex.IsMatch(
            line,
            @"\b(?:tel|telefon|phone|fon)\b\.?",
            RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeMobileNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value,
            @"(?:\+49|0049)\s*\(?0?\)?\s*\(?1[5-7]\d{1,3}\)?|\b01[5-7]\d{1,3}",
            RegexOptions.IgnoreCase);
    }

    private static bool HasPhoneLabelForNumber(string? number, IEnumerable<string> lines)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return false;
        }

        var normalized = NormalizeNumber(number);
        return lines.Any(line =>
            HasPhoneLabel(line) &&
            NormalizeNumber(line).Contains(normalized, StringComparison.Ordinal));
    }

    private static bool IsSameNumber(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) ||
            string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            NormalizeNumber(left),
            NormalizeNumber(right),
            StringComparison.Ordinal);
    }

    private static string FormatResultJson(BusinessCardAiResult result)
    {
        return JsonSerializer.Serialize(new
        {
            firstName = result.FirstName,
            lastName = result.LastName,
            company = result.Company,
            jobTitle = result.JobTitle,
            email = result.Email,
            phone = result.Phone,
            mobile = result.Mobile,
            website = result.Website,
            street = result.Street,
            zipCode = result.ZipCode,
            city = result.City,
            country = result.Country
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}
