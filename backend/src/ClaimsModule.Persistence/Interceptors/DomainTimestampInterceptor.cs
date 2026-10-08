using ClaimsModule.Domain.Entities;
using ClaimsModule.Persistence.Configurations;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClaimsModule.Persistence.Interceptors;

/// <summary>Preserves existing Domain CreatedAt APIs while the single database CreatedAt column is shadow state.
/// AuditCreatedAt is only an EF model alias; there is no second timestamp column.</summary>
internal sealed class DomainTimestampInterceptor : IMaterializationInterceptor
{
    internal static readonly DomainTimestampInterceptor Instance = new();

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is ReserveHistory or ClaimValidationIssue)
            entity.GetType().GetProperty("CreatedAt")!.SetValue(entity,
                materializationData.GetPropertyValue<DateTimeOffset>(Mapping.DomainCreatedAt));
        return entity;
    }
}
