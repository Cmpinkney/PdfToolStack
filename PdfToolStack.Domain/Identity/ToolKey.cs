namespace PdfToolStack.Domain.Identity;

public sealed record ToolKey
{
    public ToolKey(string value)
    {
        Value = Normalize(value, nameof(value));
    }

    public string Value { get; }

    public static ToolKey From(string value) => new(value);

    public override string ToString() => Value;

    private static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Tool key cannot be empty.", parameterName);

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("Tool key contains unsupported characters.", parameterName);

        return normalized;
    }
}
