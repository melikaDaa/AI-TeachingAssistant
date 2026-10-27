using System.Text;
using System.Text.RegularExpressions;

namespace AI.TeachingAssistant.Infrastructure.Security;

public static partial class PhoneNumberNormalizer
{
    [GeneratedRegex("[^0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex NonDigit();

    public static string? NormalizeIranianMobile(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var normalized = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c is >= '\u06F0' and <= '\u06F9') normalized.Append((char)('0' + c - '\u06F0'));
            else if (c is >= '\u0660' and <= '\u0669') normalized.Append((char)('0' + c - '\u0660'));
            else normalized.Append(c);
        }

        var digits = NonDigit().Replace(normalized.ToString(), string.Empty);
        if (digits.StartsWith("0098", StringComparison.Ordinal)) digits = digits[2..];
        if (digits.StartsWith("98", StringComparison.Ordinal)) digits = "+" + digits;
        else if (digits.StartsWith('0')) digits = "+98" + digits[1..];
        else if (digits.StartsWith('9')) digits = "+98" + digits;
        else if (digits.StartsWith('+')) { }
        else return null;

        return Regex.IsMatch(digits, @"^\+989\d{9}$", RegexOptions.CultureInvariant) ? digits : null;
    }
}
