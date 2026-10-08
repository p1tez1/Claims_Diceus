using ClaimsModule.Application.Abstractions;
using ClaimsModule.Persistence.Queries;
using ClaimsModule.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClaimsModule.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IIdentifierGenerator, SequentialIdentifierGenerator>();
        services.AddDbContext<ClaimsDbContext>((provider, options) => options.UseSqlServer(
            provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured."), sql =>
                sql.EnableRetryOnFailure(maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));
        // The API supplies ICurrentUser when authentication is implemented. Do not invent a default tenant.
        services.RemoveAll<ClaimsDbContext>();
        services.AddScoped(provider => new ClaimsDbContext(provider.GetRequiredService<DbContextOptions<ClaimsDbContext>>(),
            provider.GetRequiredService<ICurrentUser>(), provider.GetRequiredService<IClock>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IReserveComponentRepository, ReserveComponentRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IClaimNumberGenerator, ClaimNumberGenerator>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IBackgroundJobStore, BackgroundJobStore>();
        services.AddScoped<ReadQueries>();
        services.AddScoped<IClaimQueries>(p => p.GetRequiredService<ReadQueries>());
        services.AddScoped<IAuditQueries>(p => p.GetRequiredService<ReadQueries>());
        services.AddScoped<IReferenceDataQueries>(p => p.GetRequiredService<ReadQueries>());
        services.AddScoped<IPolicyQueries>(p => p.GetRequiredService<ReadQueries>());
        services.AddScoped<IUserQueries>(p => p.GetRequiredService<ReadQueries>());
        services.AddScoped<IDocumentQueries>(p => p.GetRequiredService<ReadQueries>());
        services.AddScoped<IReserveQueries>(p => p.GetRequiredService<ReadQueries>());
        return services;
    }
}
