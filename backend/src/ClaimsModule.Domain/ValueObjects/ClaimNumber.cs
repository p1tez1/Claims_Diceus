using System.Globalization;
using ClaimsModule.Domain.Exceptions;

namespace ClaimsModule.Domain.ValueObjects;

public sealed record ClaimNumber
{
    private ClaimNumber(int year, int sequence)
    {
        Year = year;
        Sequence = sequence;
    }

    public int Year { get; }
    public int Sequence { get; }
    public string Value => string.Create(CultureInfo.InvariantCulture, $"CLM-{Year:D4}-{Sequence:D7}");

    public static ClaimNumber Create(int year, int sequence)
    {
        if (year is < 1 or > 9999 || sequence is < 1 or > 9_999_999)
        {
            throw new DomainRuleViolationException("BR-C-04", "Claim number requires a four-digit year and a sequence from 1 to 9999999.");
        }

        return new ClaimNumber(year, sequence);
    }

    public static ClaimNumber Parse(string value)
    {
        if (value is null || value.Length != 16 || !value.StartsWith("CLM-", StringComparison.Ordinal)
            || value[8] != '-' || !value.AsSpan(4, 4).ContainsOnlyAsciiDigits()
            || !value.AsSpan(9, 7).ContainsOnlyAsciiDigits())
        {
            throw new DomainRuleViolationException("BR-C-04", "Claim number must use CLM-{YYYY}-{7-digit-sequence}.");
        }

        return Create(int.Parse(value.AsSpan(4, 4), CultureInfo.InvariantCulture), int.Parse(value.AsSpan(9, 7), CultureInfo.InvariantCulture));
    }

    public override string ToString() => Value;
}

internal static class ClaimNumberDigits
{
    internal static bool ContainsOnlyAsciiDigits(this ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (character is < '0' or > '9') return false;
        }

        return true;
    }
}
