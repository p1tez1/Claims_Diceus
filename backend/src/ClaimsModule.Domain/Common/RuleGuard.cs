using ClaimsModule.Domain.Exceptions;

namespace ClaimsModule.Domain.Common;

internal static class RuleGuard
{
    internal static Guid Id(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleViolationException("INVALID_ID", $"{name} must not be empty.");
        }

        return value;
    }

    internal static string Text(string? value, string name, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            throw new DomainRuleViolationException("INVALID_FIELD", $"{name} is required and must not exceed {maxLength} characters.");
        }

        return value.Trim();
    }

    internal static T Defined<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new DomainRuleViolationException("INVALID_ENUM", $"Invalid {typeof(T).Name}.");
        }

        return value;
    }

    internal static void Money(decimal value)
    {
        if (value < -999_999_999_999_999.9999m || value > 999_999_999_999_999.9999m || decimal.Round(value, 4) != value)
        {
            throw new DomainRuleViolationException("INVALID_AMOUNT", "Amounts must fit decimal(19,4) without rounding.");
        }
    }
}
