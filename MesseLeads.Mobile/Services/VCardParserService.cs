using System.Text;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class VCardParserService
{
    public QrContactData Parse(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return new QrContactData();
        }

        var normalized = NormalizeLineEndings(rawContent.Trim());
        var lines = UnfoldLines(normalized);

        string? firstName = null;
        string? lastName = null;
        string? formattedName = null;
        string? company = null;
        string? jobTitle = null;
        string? email = null;
        string? phone = null;
        string? mobile = null;
        string? website = null;
        string? street = null;
        string? zipCode = null;
        string? city = null;
        string? country = null;

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var descriptor = line[..separatorIndex];
            var value = DecodeValue(line[(separatorIndex + 1)..]);
            var propertyName = descriptor.Split(';', 2)[0].Trim().ToUpperInvariant();
            var descriptorUpper = descriptor.ToUpperInvariant();

            switch (propertyName)
            {
                case "N":
                {
                    var parts = SplitEscaped(value, ';');
                    lastName = GetPart(parts, 0);
                    firstName = JoinNonEmpty(GetPart(parts, 1), GetPart(parts, 2));
                    break;
                }
                case "FN":
                    formattedName = value;
                    break;
                case "ORG":
                    company = FirstNonEmpty(SplitEscaped(value, ';'));
                    break;
                case "TITLE":
                    jobTitle = value;
                    break;
                case "EMAIL":
                    email ??= value;
                    break;
                case "TEL":
                    if (IsMobileTelephone(descriptorUpper))
                    {
                        mobile ??= value;
                    }
                    else
                    {
                        phone ??= value;
                    }
                    break;
                case "URL":
                    website ??= NormalizeWebsite(value);
                    break;
                case "ADR":
                {
                    var parts = SplitEscaped(value, ';');
                    street ??= GetPart(parts, 2);
                    city ??= GetPart(parts, 3);
                    zipCode ??= GetPart(parts, 5);
                    country ??= GetPart(parts, 6);
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(firstName) &&
            string.IsNullOrWhiteSpace(lastName) &&
            !string.IsNullOrWhiteSpace(formattedName))
        {
            (firstName, lastName) = SplitFormattedName(formattedName);
        }

        if (string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(mobile))
        {
            phone = mobile;
        }

        return new QrContactData
        {
            RawContent = rawContent,
            FirstName = NullIfWhiteSpace(firstName),
            LastName = NullIfWhiteSpace(lastName),
            Company = NullIfWhiteSpace(company),
            JobTitle = NullIfWhiteSpace(jobTitle),
            Email = NullIfWhiteSpace(email),
            Phone = NullIfWhiteSpace(phone),
            Mobile = NullIfWhiteSpace(mobile),
            Website = NullIfWhiteSpace(website),
            Street = NullIfWhiteSpace(street),
            ZipCode = NullIfWhiteSpace(zipCode),
            City = NullIfWhiteSpace(city),
            Country = NullIfWhiteSpace(country)
        };
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
             .Replace('\r', '\n');

    private static IReadOnlyList<string> UnfoldLines(string value)
    {
        var result = new List<string>();

        foreach (var line in value.Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && result.Count > 0)
            {
                result[^1] += line[1..];
            }
            else
            {
                result.Add(line.TrimEnd());
            }
        }

        return result;
    }

    private static string DecodeValue(string value)
    {
        var decoded = value.Trim();

        if (decoded.Contains('=', StringComparison.Ordinal))
        {
            decoded = DecodeQuotedPrintable(decoded);
        }

        return decoded
            .Replace("\\n", Environment.NewLine, StringComparison.OrdinalIgnoreCase)
            .Replace("\\;", ";", StringComparison.Ordinal)
            .Replace("\\,", ",", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Trim();
    }

    private static string DecodeQuotedPrintable(string value)
    {
        var bytes = new List<byte>();

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '=' && index + 2 < value.Length &&
                byte.TryParse(value.AsSpan(index + 1, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedByte))
            {
                bytes.Add(parsedByte);
                index += 2;
                continue;
            }

            bytes.AddRange(Encoding.UTF8.GetBytes(value[index].ToString()));
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static string[] SplitEscaped(string value, char separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var escaped = false;

        foreach (var character in value)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                current.Append(character);
                continue;
            }

            if (character == separator)
            {
                parts.Add(DecodeValue(current.ToString()));
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        parts.Add(DecodeValue(current.ToString()));
        return parts.ToArray();
    }

    private static bool IsMobileTelephone(string descriptor) =>
        descriptor.Contains("CELL", StringComparison.OrdinalIgnoreCase) ||
        descriptor.Contains("MOBILE", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeWebsite(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"https://{trimmed}";
    }

    private static (string? FirstName, string? LastName) SplitFormattedName(string formattedName)
    {
        var parts = formattedName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (string.Join(' ', parts[..^1]), parts[^1])
        };
    }

    private static string? GetPart(IReadOnlyList<string> parts, int index) =>
        index < parts.Count ? NullIfWhiteSpace(parts[index]) : null;

    private static string? FirstNonEmpty(IEnumerable<string> values) =>
        values.Select(NullIfWhiteSpace).FirstOrDefault(value => value is not null);

    private static string? JoinNonEmpty(params string?[] values)
    {
        var result = string.Join(' ', values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return NullIfWhiteSpace(result);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
