namespace Scholar.Domain.Common;

/// <summary>Postal address stored inline on the owning row (EF Core complex type).</summary>
public sealed record Address
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? Province { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryCode { get; init; }

    public static Address Empty { get; } = new();

    public string ToSingleLine() =>
        string.Join(", ", new[] { Line1, Line2, City, Province, PostalCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
