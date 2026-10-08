using System.Text.Json;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;
using ClaimsModule.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace ClaimsModule.Persistence.Configurations;

internal static class Mapping
{
    internal const string DomainCreatedAt = "AuditCreatedAt";

    internal static void Key<T>(EntityTypeBuilder<T> builder, string table, string key) where T : class
    {
        builder.ToTable(table);
        builder.HasKey("Id");
        builder.Property<Guid>("Id").HasColumnName(key).HasDefaultValueSql("NEWSEQUENTIALID()")
            .HasValueGenerator<SequentialGuidValueGenerator>();
        // Domain factories always supply IDs. Mark them assigned so newly discovered children are INSERTs,
        // rather than updates of supposedly existing entities with a generated, already populated key.
        if (typeof(T).Namespace == typeof(Claim).Namespace) builder.Property<Guid>("Id").ValueGeneratedNever();
    }

    internal static void Aggregate<T>(EntityTypeBuilder<T> builder) where T : AggregateRoot
    {
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.RowVersion).UsePropertyAccessMode(PropertyAccessMode.Property)
            .HasColumnName("RowVer").IsRowVersion();
    }

    internal static void ClaimReference<T>(EntityTypeBuilder<T> builder) where T : class => builder.HasOne<Claim>()
        .WithMany().HasForeignKey("ClaimId", "OrganisationId").HasPrincipalKey("Id", "OrganisationId")
        .OnDelete(DeleteBehavior.NoAction);

    internal static void StandardColumns(ModelBuilder model)
    {
        foreach (var entity in model.Model.GetEntityTypes().ToArray())
        {
            var builder = model.Entity(entity.ClrType);
            var createdAt = entity.ClrType == typeof(ReserveHistory) || entity.ClrType == typeof(ClaimValidationIssue)
                ? DomainCreatedAt : "CreatedAt";
            builder.Property<DateTimeOffset>(createdAt).HasColumnName("CreatedAt").HasPrecision(7);
            builder.Property<DateTimeOffset?>("UpdatedAt").HasPrecision(7);
            builder.Property<Guid?>("UserCreated");
            builder.Property<Guid?>("UserModified");
            builder.Property<Guid>("OrganisationId");

            foreach (var p in entity.GetProperties().ToArray())
            {
                var type = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
                var property = builder.Property(p.Name);
                if (type == typeof(decimal)) property.HasPrecision(19, 4);
                if (type == typeof(DateTimeOffset)) property.HasPrecision(7);
                if (type.IsEnum)
                {
                    property.HasConversion<string>();
                    if (p.GetMaxLength() is null) property.HasMaxLength(50);
                }
                if (type == typeof(bool)) property.HasDefaultValue(false);
            }
        }
    }
}

public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        Mapping.Key(builder, "Claims", "ClaimId");
        Mapping.Aggregate(builder);
        builder.Ignore(x => x.LastActivity);
        builder.Property(x => x.ClaimNumber).HasConversion(x => x.Value, x => ClaimNumber.Parse(x)).HasMaxLength(50);
        builder.Property(x => x.PolicyNumber).HasMaxLength(50);
        builder.Property(x => x.ClientName).HasMaxLength(255);
        builder.Property(x => x.LiabilityDetermination);
        builder.Property(x => x.LiabilityDeterminedAt);
        builder.HasIndex("OrganisationId", nameof(Claim.ClaimNumber)).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.Property<DateTimeOffset?>("UpdatedAt");
        builder.HasIndex("UpdatedAt");
        builder.HasOne<Policy>().WithMany().HasForeignKey("PolicyId", "OrganisationId")
            .HasPrincipalKey("Id", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey("AssignedHandlerId", "OrganisationId")
            .HasPrincipalKey("Id", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.LossEvent).WithOne().HasForeignKey<LossEvent>("ClaimId", "OrganisationId")
            .HasPrincipalKey<Claim>("Id", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(x => x.Parties).WithOne().HasForeignKey("ClaimId", "OrganisationId")
            .HasPrincipalKey("Id", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(x => x.RiskObjects).WithOne().HasForeignKey("ClaimId", "OrganisationId")
            .HasPrincipalKey("Id", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(x => x.ValidationIssues).WithOne().HasForeignKey("ClaimId", "OrganisationId")
            .HasPrincipalKey("Id", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
        builder.Navigation(x => x.Parties).HasField("_parties").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.RiskObjects).HasField("_riskObjects").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.ValidationIssues).HasField("_validationIssues").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class LossEventConfiguration : IEntityTypeConfiguration<LossEvent>
{
    public void Configure(EntityTypeBuilder<LossEvent> builder)
    {
        Mapping.Key(builder, "LossEvents", "LossEventId");
        builder.Property(x => x.LossLocation).HasMaxLength(500);
        builder.Property(x => x.CauseOfLossCode).HasMaxLength(50);
        builder.Property(x => x.PoliceReportNumber).HasMaxLength(100);
        builder.HasOne<CauseOfLossCode>().WithMany().HasForeignKey("CauseOfLossCode", "OrganisationId")
            .HasPrincipalKey("Code", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ClaimPartyConfiguration : IEntityTypeConfiguration<ClaimParty>
{
    public void Configure(EntityTypeBuilder<ClaimParty> builder)
    {
        Mapping.Key(builder, "ClaimParties", "ClaimPartyId");
        builder.Property(x => x.Role).HasColumnName("PartyRole");
        builder.Property(x => x.Type).HasColumnName("PartyType").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.CompanyName).HasMaxLength(255);
        builder.Property(x => x.Email).HasMaxLength(255);
        builder.Property(x => x.Phone).HasMaxLength(50);
    }
}

public sealed class ClaimRiskObjectConfiguration : IEntityTypeConfiguration<ClaimRiskObject>
{
    public void Configure(EntityTypeBuilder<ClaimRiskObject> builder)
    {
        Mapping.Key(builder, "ClaimRiskObjects", "ClaimRiskObjectId");
        builder.Property(x => x.AssetDescription).HasMaxLength(500);
        builder.Property(x => x.AssetReference).HasMaxLength(255);
    }
}

public sealed class ClaimValidationIssueConfiguration : IEntityTypeConfiguration<ClaimValidationIssue>
{
    public void Configure(EntityTypeBuilder<ClaimValidationIssue> builder)
    {
        Mapping.Key(builder, "ClaimValidationIssues", "ClaimValidationIssueId");
        builder.Ignore(x => x.CreatedAt);
        builder.Property(x => x.Code).HasMaxLength(50);
    }
}

public sealed class ClaimReserveComponentConfiguration : IEntityTypeConfiguration<ClaimReserveComponent>
{
    public void Configure(EntityTypeBuilder<ClaimReserveComponent> builder)
    {
        Mapping.Key(builder, "ClaimReserveComponents", "ReserveComponentId");
        Mapping.Aggregate(builder);
        Mapping.ClaimReference(builder);
        builder.Ignore(x => x.HasPendingTransactions);
        builder.Ignore(x => x.HasApprovedTransactions);
        builder.Ignore(x => x.PendingAmount);
        builder.Property<string?>("Notes");
        builder.HasIndex(x => new { x.ClaimId, x.Component }).IsUnique();
        builder.HasMany(x => x.ReserveHistory).WithOne().HasForeignKey("ReserveComponentId", "ClaimId", "OrganisationId")
            .HasPrincipalKey("Id", "ClaimId", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
        builder.Navigation(x => x.ReserveHistory).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ReserveHistoryConfiguration : IEntityTypeConfiguration<ReserveHistory>
{
    public void Configure(EntityTypeBuilder<ReserveHistory> builder)
    {
        Mapping.Key(builder, "ReserveHistory", "ReserveHistoryId");
        Mapping.ClaimReference(builder);
        builder.Ignore(x => x.CreatedAt);
        builder.Ignore(x => x.IsApproved);
        builder.Property(x => x.ChangeReason).HasMaxLength(500);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.PostingJobId).HasMaxLength(255);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.ReserveComponentId, x.ChangeSequence }).IsUnique();
        builder.HasOne<ReserveHistory>().WithMany().HasForeignKey("ResubmissionOfId", "ReserveComponentId", "OrganisationId")
            .HasPrincipalKey("Id", "ReserveComponentId", "OrganisationId").OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ClaimDocumentConfiguration : IEntityTypeConfiguration<ClaimDocument>
{
    public void Configure(EntityTypeBuilder<ClaimDocument> builder)
    {
        Mapping.Key(builder, "ClaimDocuments", "ClaimDocumentId");
        Mapping.ClaimReference(builder);
        builder.Property(x => x.DocumentType).HasMaxLength(100);
        builder.Property(x => x.DocumentName).HasMaxLength(255);
        builder.Property(x => x.BlobPath).HasMaxLength(500);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);
    }
}

public sealed class ClaimAuditLogConfiguration : IEntityTypeConfiguration<ClaimAuditLog>
{
    public void Configure(EntityTypeBuilder<ClaimAuditLog> builder)
    {
        Mapping.Key(builder, "ClaimAuditLog", "AuditLogId");
        Mapping.ClaimReference(builder);
        builder.Property(x => x.EventType).HasMaxLength(100);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(255);
        builder.Property<DateTimeOffset>("CreatedAt");
        builder.HasIndex("ClaimId", "CreatedAt");
    }
}

public sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        Mapping.Key(builder, "Policies", "PolicyId");
        builder.Property(x => x.PolicyNumber).HasMaxLength(50);
        builder.Property(x => x.ClientName).HasMaxLength(255);
        builder.HasIndex(x => x.PolicyNumber).IsUnique();
        builder.Property(x => x.CoverageTypes).HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<string[]>(value, (JsonSerializerOptions?)null)!)
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (a, c) => a!.SequenceEqual(c!),
                value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                value => value.ToArray()));
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        Mapping.Key(builder, "Users", "UserId");
        builder.Property(x => x.Username).HasMaxLength(255);
        builder.Property(x => x.Name).HasMaxLength(255);
        builder.HasIndex("OrganisationId", nameof(User.Username)).IsUnique();
    }
}

public sealed class CauseOfLossCodeConfiguration : IEntityTypeConfiguration<CauseOfLossCode>
{
    public void Configure(EntityTypeBuilder<CauseOfLossCode> builder)
    {
        Mapping.Key(builder, "CauseOfLossCodes", "CauseOfLossCodeId");
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.PerilCategory).HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class ClaimStatusTransitionConfiguration : IEntityTypeConfiguration<ClaimStatusTransitionRow>
{
    public void Configure(EntityTypeBuilder<ClaimStatusTransitionRow> builder)
    {
        Mapping.Key(builder, "ClaimStatusTransitions", "ClaimStatusTransitionId");
        builder.Property(x => x.RuleCode).HasMaxLength(50);
        builder.HasIndex("OrganisationId", "From", "To").IsUnique();
    }
}

public sealed class ClaimNumberSequenceConfiguration : IEntityTypeConfiguration<ClaimNumberSequence>
{
    public void Configure(EntityTypeBuilder<ClaimNumberSequence> builder)
    {
        Mapping.Key(builder, "ClaimNumberSequences", "ClaimNumberSequenceId");
        builder.HasIndex("OrganisationId", nameof(ClaimNumberSequence.Year)).IsUnique();
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        Mapping.Key(builder, "IdempotencyRecords", "IdempotencyRecordId");
        builder.Property(x => x.Key).HasMaxLength(200);
        builder.Property(x => x.HttpMethod).HasMaxLength(50);
        builder.Property(x => x.RouteTemplate).HasMaxLength(500);
        builder.Property(x => x.RequestHash).HasMaxLength(64);
        builder.HasIndex("OrganisationId", nameof(IdempotencyRecord.UserId), nameof(IdempotencyRecord.Key)).IsUnique();
    }
}
