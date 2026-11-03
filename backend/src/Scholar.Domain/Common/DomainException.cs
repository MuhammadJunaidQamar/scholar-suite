namespace Scholar.Domain.Common;

/// <summary>
/// A business rule was violated. Carries a stable machine-readable <see cref="Code"/> (e.g. <c>invoice.already_paid</c>)
/// so clients can react without parsing the human message. Surfaces as HTTP 422.
/// </summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class Guard
{
    public static void Against(bool condition, string code, string message)
    {
        if (condition)
        {
            throw new DomainException(code, message);
        }
    }

    public static decimal NotNegative(decimal value, string field) =>
        value < 0 ? throw new DomainException("value.negative", $"{field} cannot be negative.") : value;

    public static decimal Positive(decimal value, string field) =>
        value <= 0 ? throw new DomainException("value.not_positive", $"{field} must be greater than zero.") : value;
}
