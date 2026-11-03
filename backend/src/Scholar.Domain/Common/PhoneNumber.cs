namespace Scholar.Domain.Common;

public static class PhoneNumber
{
    /// <summary>Normalises Pakistani mobile numbers to +92XXXXXXXXXX so the same person is recognised however it is typed.</summary>
    public static string Normalize(string phone, string errorCode = "phone.invalid")
    {
        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        Guard.Against(digits.Count(char.IsDigit) < 7, errorCode, "Enter a valid phone number.");
        if (digits.StartsWith("+", StringComparison.Ordinal)) return digits;
        if (digits.StartsWith("0092", StringComparison.Ordinal)) return "+" + digits[2..];
        if (digits.StartsWith("92", StringComparison.Ordinal) && digits.Length == 12) return "+" + digits;
        if (digits.StartsWith("03", StringComparison.Ordinal) && digits.Length == 11) return "+92" + digits[1..];
        return digits;
    }
}
