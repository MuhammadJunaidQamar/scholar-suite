using System.Globalization;
using System.Text.RegularExpressions;

namespace Scholar.Domain.Platform;

/// <summary>Renders numbering patterns. Tokens: {prefix} {yyyy} {yy} {mm} {seq} {seq:N} (N = zero-padded width).</summary>
public static partial class SequencePattern
{
    public static string Format(string pattern, string prefix, long value, DateOnly date)
    {
        var text = pattern
            .Replace("{prefix}", prefix, StringComparison.Ordinal)
            .Replace("{yyyy}", date.Year.ToString("D4", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{yy}", (date.Year % 100).ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{mm}", date.Month.ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return SeqToken().Replace(text, m =>
        {
            var width = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
            return value.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
        });
    }

    [GeneratedRegex(@"\{seq(?::(\d{1,2}))?\}")]
    private static partial Regex SeqToken();
}
