using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClaimsModule.Persistence;

public sealed class ClaimsDbContextFactory : IDesignTimeDbContextFactory<ClaimsDbContext>
{
    public ClaimsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Database=ClaimsModule;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(connectionString).Options;
        return new(options, new DesignTimeUser(), new SystemClock());
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string Name => "Migration tooling";
        public UserRole? Role => null;
        public Guid OrganisationId => Guid.Empty;
        public Guid? CorrelationId => null;
    }
}
