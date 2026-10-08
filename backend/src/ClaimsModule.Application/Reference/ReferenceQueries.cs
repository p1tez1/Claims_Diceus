using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Reference;

public sealed record GetCauseOfLossCodesQuery(string? PerilCategory) : IQuery<IReadOnlyList<CauseOfLossDto>>;

public sealed class GetCauseOfLossCodesQueryHandler(IReferenceDataQueries queries)
    : IRequestHandler<GetCauseOfLossCodesQuery, IReadOnlyList<CauseOfLossDto>>
{
    public Task<IReadOnlyList<CauseOfLossDto>> Handle(GetCauseOfLossCodesQuery request, CancellationToken cancellationToken) =>
        queries.GetCausesAsync(request.PerilCategory, cancellationToken);
}

public sealed record GetClaimStatusesQuery : IQuery<IReadOnlyList<ClaimStatusDto>>;

public sealed class GetClaimStatusesQueryHandler(IReferenceDataQueries queries)
    : IRequestHandler<GetClaimStatusesQuery, IReadOnlyList<ClaimStatusDto>>
{
    public Task<IReadOnlyList<ClaimStatusDto>> Handle(GetClaimStatusesQuery request, CancellationToken cancellationToken) =>
        queries.GetStatusesAsync(cancellationToken);
}

public sealed record GetUsersQuery(UserRole? Role) : IQuery<IReadOnlyList<UserDto>>;

public sealed class GetUsersQueryHandler(IUserQueries queries)
    : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken) =>
        queries.ListAsync(request.Role, cancellationToken);
}

public sealed record SearchPoliciesQuery(string? Search) : IQuery<IReadOnlyList<PolicyDto>>;

public sealed class SearchPoliciesQueryHandler(IPolicyQueries queries)
    : IRequestHandler<SearchPoliciesQuery, IReadOnlyList<PolicyDto>>
{
    public Task<IReadOnlyList<PolicyDto>> Handle(SearchPoliciesQuery request, CancellationToken cancellationToken) =>
        queries.SearchAsync(request.Search ?? string.Empty, cancellationToken);
}

public sealed record GetPolicyCoverageQuery(Guid PolicyId) : IQuery<IReadOnlyList<string>>;

public sealed class GetPolicyCoverageQueryHandler(IPolicyQueries queries)
    : IRequestHandler<GetPolicyCoverageQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetPolicyCoverageQuery request, CancellationToken cancellationToken)
    {
        var policy = await queries.GetAsync(request.PolicyId, cancellationToken);
        if (policy is null) throw new NotFoundException($"Policy '{request.PolicyId}' was not found.");
        return policy.CoverageTypes;
    }
}
