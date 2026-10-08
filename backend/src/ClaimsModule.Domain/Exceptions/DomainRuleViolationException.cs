namespace ClaimsModule.Domain.Exceptions;

public class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class SelfApprovalException : DomainRuleViolationException
{
    public SelfApprovalException() : base("BR-R-03", "Self-approval is not permitted.") { }
}

public sealed class InsufficientAuthorityException : DomainRuleViolationException
{
    public InsufficientAuthorityException(string message = "Your role does not have authority to approve this reserve amount.")
        : base("INSUFFICIENT_AUTHORITY", message) { }
}
