using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;

namespace ClaimsModule.Domain.Rules;

public sealed record ReserveAuthorityLimits(
    decimal AutoApproval = 10_000m,
    decimal Supervisor = 100_000m,
    decimal Manager = 10_000_000m);

public sealed class ReserveAuthorityPolicy
{
    public const decimal AutoApprovalThreshold = 10_000m;
    public const decimal SupervisorThreshold = 100_000m;
    public const decimal ManagerThreshold = 10_000_000m;
    public const decimal AggregateCap = 10_000_000m;

    public ReserveAuthorityPolicy(ReserveAuthorityLimits? limits = null)
    {
        Limits = limits ?? new ReserveAuthorityLimits();
        if (Limits.AutoApproval <= 0 || Limits.Supervisor < Limits.AutoApproval || Limits.Manager < Limits.Supervisor)
        {
            throw new ArgumentException("Authority limits must be positive and ordered.", nameof(limits));
        }
    }

    public ReserveAuthorityLimits Limits { get; }

    public UserRole? RequiredApprover(decimal delta)
    {
        RuleGuard.Money(delta);
        var magnitude = Math.Abs(delta);
        return magnitude <= Limits.AutoApproval ? null
            : magnitude <= Limits.Supervisor ? UserRole.Supervisor : UserRole.Manager;
    }

    public bool CanApprove(decimal delta, UserRole role)
    {
        RuleGuard.Defined(role);
        RuleGuard.Money(delta);
        var magnitude = Math.Abs(delta);
        return role switch
        {
            UserRole.Supervisor => magnitude <= Limits.Supervisor,
            UserRole.Manager => magnitude <= Limits.Manager,
            _ => false,
        };
    }

    public void EnsureCanApprove(decimal delta, UserRole role)
    {
        if (!CanApprove(delta, role)) throw new InsufficientAuthorityException();
    }
}
