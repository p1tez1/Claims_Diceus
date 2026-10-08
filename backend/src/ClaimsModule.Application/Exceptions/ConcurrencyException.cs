namespace ClaimsModule.Application.Exceptions;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>A SQL deadlock rolled back the whole owning transaction. Retry the entire operation, not just allocation.</summary>
public sealed class TransactionRetryRequiredException : Exception
{
    public TransactionRetryRequiredException(string message, Exception innerException) : base(message, innerException) { }
}
