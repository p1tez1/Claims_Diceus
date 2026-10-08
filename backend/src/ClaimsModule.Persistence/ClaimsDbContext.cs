using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Events;
using ClaimsModule.Persistence.Configurations;
using ClaimsModule.Persistence.Entities;
using ClaimsModule.Persistence.Interceptors;
using ClaimsModule.Persistence.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ClaimsModule.Persistence;

public sealed class ClaimsDbContext : DbContext
{
    private readonly ICurrentUser _user;
    internal Domain.Common.IClock Clock { get; }
    internal Dictionary<Claim, ActivityMarker?> ActivitySnapshots { get; } = new(ReferenceEqualityComparer.Instance);
    internal HashSet<ClaimAuditLog> AuthorizedAuditEntries { get; } = new(ReferenceEqualityComparer.Instance);
    internal bool TransactionAbortedByServer { get; set; }
    public Guid TenantId => _user.OrganisationId;
    internal ICurrentUser CurrentUser => _user;

    public ClaimsDbContext(DbContextOptions<ClaimsDbContext> options, ICurrentUser user, Application.Abstractions.IClock clock)
        : base(options)
    {
        _user = user;
        Clock = clock;
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;
        ChangeTracker.Tracked += (_, args) =>
        {
            if (args.Entry.State == EntityState.Added)
                args.Entry.Property("OrganisationId").CurrentValue = TenantId;
            if (args.Entry.Entity is Claim claim)
                ActivitySnapshots[claim] = args.FromQuery ? claim.LastActivity : null;
        };
    }

    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<LossEvent> LossEvents => Set<LossEvent>();
    public DbSet<ClaimParty> ClaimParties => Set<ClaimParty>();
    public DbSet<ClaimRiskObject> ClaimRiskObjects => Set<ClaimRiskObject>();
    public DbSet<ClaimValidationIssue> ClaimValidationIssues => Set<ClaimValidationIssue>();
    public DbSet<ClaimReserveComponent> ClaimReserveComponents => Set<ClaimReserveComponent>();
    public DbSet<ReserveHistory> ReserveHistory => Set<ReserveHistory>();
    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();
    public DbSet<ClaimAuditLog> ClaimAuditLog => Set<ClaimAuditLog>();
    public DbSet<CauseOfLossCode> CauseOfLossCodes => Set<CauseOfLossCode>();
    public DbSet<ClaimStatusTransitionRow> ClaimStatusTransitions => Set<ClaimStatusTransitionRow>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ClaimNumberSequence> ClaimNumberSequences => Set<ClaimNumberSequence>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder
        .AddInterceptors(AuditSaveChangesInterceptor.Instance, DomainTimestampInterceptor.Instance, AuditQueryInterceptor.Instance);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<AggregateRoot>();
        modelBuilder.Ignore<Domain.ValueObjects.ClaimNumber>();
        modelBuilder.Ignore<ActivityMarker>();
        TenantFilter<Claim>(modelBuilder, true);
        TenantFilter<LossEvent>(modelBuilder, true);
        TenantFilter<ClaimParty>(modelBuilder, true);
        TenantFilter<ClaimRiskObject>(modelBuilder, true);
        TenantFilter<ClaimValidationIssue>(modelBuilder, true);
        TenantFilter<ClaimDocument>(modelBuilder, true);
        TenantFilter<ClaimReserveComponent>(modelBuilder, false);
        TenantFilter<ReserveHistory>(modelBuilder, false);
        TenantFilter<ClaimAuditLog>(modelBuilder, false);
        TenantFilter<Policy>(modelBuilder, false);
        TenantFilter<User>(modelBuilder, false);
        TenantFilter<CauseOfLossCode>(modelBuilder, false);
        TenantFilter<ClaimStatusTransitionRow>(modelBuilder, false);
        TenantFilter<ClaimNumberSequence>(modelBuilder, false);
        TenantFilter<IdempotencyRecord>(modelBuilder, false);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);
        Mapping.StandardColumns(modelBuilder);
        SeedData.Configure(modelBuilder);
    }

    private void TenantFilter<T>(ModelBuilder model, bool softDelete) where T : class
    {
        var b = model.Entity<T>();
        b.Property<Guid>("OrganisationId");
        if (softDelete)
        {
            b.Property<bool>("IsDeleted").HasDefaultValue(false);
            b.Property<DateTimeOffset?>("DeletedAt").HasPrecision(7);
            b.HasQueryFilter(row => !EF.Property<bool>(row, "IsDeleted") && EF.Property<Guid>(row, "OrganisationId") == TenantId);
        }
        else b.HasQueryFilter(row => EF.Property<Guid>(row, "OrganisationId") == TenantId);
    }

    public IReadOnlyList<IDomainEvent> CollectDomainEvents() =>
        ChangeTracker.Entries<AggregateRoot>().SelectMany(entry => entry.Entity.DomainEvents).ToArray();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch (DbUpdateConcurrencyException ex) { throw Conflict(ex); }
        catch (DbUpdateException ex) when (IsConcurrentComponentInsert(ex)) { throw ComponentInsertConflict(ex); }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch (DbUpdateConcurrencyException ex) { throw Conflict(ex); }
        catch (DbUpdateException ex) when (IsConcurrentComponentInsert(ex)) { throw ComponentInsertConflict(ex); }
    }

    // On first use there is no component RowVer yet. A competing INSERT can hit the unique
    // component index before the Claim RowVer UPDATE. Reuse DD-35's reload/re-evaluate retry.
    private static bool IsConcurrentComponentInsert(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql
        && sql.Message.Contains("IX_ClaimReserveComponents_ClaimId_Component", StringComparison.Ordinal)
        && exception.Entries.Any(e => e.Entity is ClaimReserveComponent && e.State == EntityState.Added);

    private static ConcurrencyException ComponentInsertConflict(DbUpdateException exception) =>
        new("Another request created this reserve component. Reload and re-evaluate the submission.", exception);

    private static ConcurrencyException Conflict(DbUpdateConcurrencyException exception) =>
        new("The claim or reserve changed in another transaction. Reload and re-evaluate the operation.", exception);
}
