using System.Linq.Expressions;
using ClaimsModule.Persistence.Entities;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClaimsModule.Persistence.Interceptors;

// ExecuteUpdate/Delete bypass SaveChanges. Refuse these operations for the append-only audit entity too.
internal sealed class AuditQueryInterceptor : IQueryExpressionInterceptor
{
    internal static readonly AuditQueryInterceptor Instance = new();

    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
    {
        new AuditMutationGuard().Visit(queryExpression);
        return queryExpression;
    }

    private sealed class AuditMutationGuard : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if ((node.Method.Name.StartsWith("ExecuteUpdate", StringComparison.Ordinal)
                || node.Method.Name.StartsWith("ExecuteDelete", StringComparison.Ordinal))
                && node.Method.GetGenericArguments().Contains(typeof(ClaimAuditLog)))
                throw new InvalidOperationException("ClaimAuditLog is append-only; bulk updates and deletes are forbidden.");
            return base.VisitMethodCall(node);
        }
    }
}
