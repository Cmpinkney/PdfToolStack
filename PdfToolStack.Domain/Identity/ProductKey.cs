namespace PdfToolStack.Domain.Identity;

public sealed record ProductKey
{
    public static ProductKey PdfToolStack { get; } = new("pdftoolstack");

    public ProductKey(string value)
    {
        Value = Normalize(value, nameof(value));
    }

    public string Value { get; }

    public static ProductKey From(string value) => new(value);

    public override string ToString() => Value;

    private static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Product key cannot be empty.", parameterName);

        return value.Trim().ToLowerInvariant();
    }
}
