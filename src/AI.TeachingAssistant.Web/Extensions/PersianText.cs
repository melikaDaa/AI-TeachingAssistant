using System.Globalization;

namespace AI.TeachingAssistant.Web.Extensions;

/// <summary>Persian formatting helpers (digits, dates, status labels).</summary>
public static class PersianText
{
    private const string Digits = "۰۱۲۳۴۵۶۷۸۹";

    public static string ToPersianDigits(this int value) => ToPersianDigits(value.ToString());

    public static string ToPersianDigits(this string value) =>
        string.Concat(value.Select(c => char.IsAsciiDigit(c) ? Digits[c - '0'] : c));

    public static string PersianDate(this DateTime utc) =>
        utc.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.GetCultureInfo("fa-IR"));

    public static string StatusLabel(string statusKey) => statusKey switch
    {
        "generating" => "در حال تولید",
        "ready" => "آماده",
        "failed" => "ناموفق",
        _ => "پیش‌نویس",
    };
}
