namespace ClaimsModule.Domain.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

// Persistence maps this business-activity marker to its shadow UpdatedAt column.
public sealed record ActivityMarker(DateTimeOffset At, long Revision);
