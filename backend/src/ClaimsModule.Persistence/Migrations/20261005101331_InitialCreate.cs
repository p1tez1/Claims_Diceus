using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClaimsModule.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CauseOfLossCodes",
                columns: table => new
                {
                    CauseOfLossCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PerilCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CauseOfLossCodes", x => x.CauseOfLossCodeId);
                    table.UniqueConstraint("AK_CauseOfLossCodes_Code_OrganisationId", x => new { x.Code, x.OrganisationId });
                });

            migrationBuilder.CreateTable(
                name: "ClaimNumberSequences",
                columns: table => new
                {
                    ClaimNumberSequenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimNumberSequences", x => x.ClaimNumberSequenceId);
                });

            migrationBuilder.CreateTable(
                name: "ClaimStatusTransitions",
                columns: table => new
                {
                    ClaimStatusTransitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    From = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    To = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiredRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStatusTransitions", x => x.ClaimStatusTransitionId);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    IdempotencyRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RouteTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.IdempotencyRecordId);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PolicyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CoverageTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.PolicyId);
                    table.UniqueConstraint("AK_Policies_PolicyId_OrganisationId", x => new { x.PolicyId, x.OrganisationId });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.UniqueConstraint("AK_Users_UserId_OrganisationId", x => new { x.UserId, x.OrganisationId });
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportedDate = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    AssignedHandlerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManagerOverride = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SlaBreachedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LiabilityDetermination = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LiabilityDeterminedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.ClaimId);
                    table.UniqueConstraint("AK_Claims_ClaimId_OrganisationId", x => new { x.ClaimId, x.OrganisationId });
                    table.ForeignKey(
                        name: "FK_Claims_Policies_PolicyId_OrganisationId",
                        columns: x => new { x.PolicyId, x.OrganisationId },
                        principalTable: "Policies",
                        principalColumns: new[] { "PolicyId", "OrganisationId" });
                    table.ForeignKey(
                        name: "FK_Claims_Users_AssignedHandlerId_OrganisationId",
                        columns: x => new { x.AssignedHandlerId, x.OrganisationId },
                        principalTable: "Users",
                        principalColumns: new[] { "UserId", "OrganisationId" });
                });

            migrationBuilder.CreateTable(
                name: "ClaimAuditLog",
                columns: table => new
                {
                    AuditLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAuditLog", x => x.AuditLogId);
                    table.ForeignKey(
                        name: "FK_ClaimAuditLog_Claims_ClaimId_OrganisationId",
                        columns: x => new { x.ClaimId, x.OrganisationId },
                        principalTable: "Claims",
                        principalColumns: new[] { "ClaimId", "OrganisationId" });
                });

            migrationBuilder.CreateTable(
                name: "ClaimDocuments",
                columns: table => new
                {
                    ClaimDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDocuments", x => x.ClaimDocumentId);
                    table.ForeignKey(
                        name: "FK_ClaimDocuments_Claims_ClaimId_OrganisationId",
                        columns: x => new { x.ClaimId, x.OrganisationId },
                        principalTable: "Claims",
                        principalColumns: new[] { "ClaimId", "OrganisationId" });
                });

            migrationBuilder.CreateTable(
                name: "ClaimParties",
                columns: table => new
                {
                    ClaimPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimParties", x => x.ClaimPartyId);
                    table.ForeignKey(
                        name: "FK_ClaimParties_Claims_ClaimId_OrganisationId",
                        columns: x => new { x.ClaimId, x.OrganisationId },
                        principalTable: "Claims",
                        principalColumns: new[] { "ClaimId", "OrganisationId" });
                });

            migrationBuilder.CreateTable(
                name: "ClaimReserveComponents",
                columns: table => new
                {
                    ReserveComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimReserveComponents", x => x.ReserveComponentId);
                    table.UniqueConstraint("AK_ClaimReserveComponents_ReserveComponentId_ClaimId_OrganisationId", x => new { x.ReserveComponentId, x.ClaimId, x.OrganisationId });
                    table.ForeignKey(
                        name: "FK_ClaimReserveComponents_Claims_ClaimId_OrganisationId",
                        columns: x => new { x.ClaimId, x.OrganisationId },
                        principalTable: "Claims",
                        principalColumns: new[] { "ClaimId", "OrganisationId" });
                });

            migrationBuilder.CreateTable(
                name: "ClaimRiskObjects",
                columns: table => new
                {
                    ClaimRiskObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DamageDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AssetReference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRiskObjects", x => x.ClaimRiskObjectId);
                    table.ForeignKey(
                        name: "FK_ClaimRiskObjects_Claims_ClaimId_OrganisationId",
                        columns: x => new { x.ClaimId, x.OrganisationId },
                        principalTable: "Claims",
                        principalColumns: new[] { "ClaimId", "OrganisationId" });
                });

            migrationBuilder.CreateTable(
                name: "ClaimValidationIssues",
                columns: table => new
                {
                    ClaimValidationIssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimValidationIssues", x => x.ClaimValidationIssueId);
                    table.ForeignKey(
                        name: "FK_ClaimValidationIssues_Claims_ClaimId_OrganisationId",
                        columns: x => new { x.ClaimId, x.OrganisationId },
                        principalTable: "Claims",
                        principalColumns: new[] { "ClaimId", "OrganisationId" });
                });

            migrationBuilder.CreateTable(
                name: "LossEvents",
                columns: table => new
                {
                    LossEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LossDate = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    LossDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LossLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CauseOfLossCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EstimatedLossAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ReportDate = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    PoliceReportNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LossEvents", x => x.LossEventId);
                    table.ForeignKey(
                        name: "FK_LossEvents_CauseOfLossCodes_CauseOfLossCode_OrganisationId",
                        columns: x => new { x.CauseOfLossCode, x.OrganisationId },
                        principalTable: "CauseOfLossCodes",
                        principalColumns: new[] { "Code", "OrganisationId" });
                    table.ForeignKey(
                        name: "FK_LossEvents_Claims_ClaimId_OrganisationId",
                        columns: x => new { x.ClaimId, x.OrganisationId },
                        principalTable: "Claims",
                        principalColumns: new[] { "ClaimId", "OrganisationId" });
                });

            migrationBuilder.CreateTable(
                name: "ReserveHistory",
                columns: table => new
                {
                    ReserveHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ReserveComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    NewBalance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ChangeSequence = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResubmissionOfId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetractedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetractedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    PostingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostingJobId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    PostingFailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    PostingFailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveHistory", x => x.ReserveHistoryId);
                    table.UniqueConstraint("AK_ReserveHistory_ReserveHistoryId_ReserveComponentId_OrganisationId", x => new { x.ReserveHistoryId, x.ReserveComponentId, x.OrganisationId });
                    table.ForeignKey(
                        name: "FK_ReserveHistory_ClaimReserveComponents_ReserveComponentId_ClaimId_OrganisationId",
                        columns: x => new { x.ReserveComponentId, x.ClaimId, x.OrganisationId },
                        principalTable: "ClaimReserveComponents",
                        principalColumns: new[] { "ReserveComponentId", "ClaimId", "OrganisationId" });
                    table.ForeignKey(
                        name: "FK_ReserveHistory_Claims_ClaimId_OrganisationId",
                        columns: x => new { x.ClaimId, x.OrganisationId },
                        principalTable: "Claims",
                        principalColumns: new[] { "ClaimId", "OrganisationId" });
                    table.ForeignKey(
                        name: "FK_ReserveHistory_ReserveHistory_ResubmissionOfId_ReserveComponentId_OrganisationId",
                        columns: x => new { x.ResubmissionOfId, x.ReserveComponentId, x.OrganisationId },
                        principalTable: "ReserveHistory",
                        principalColumns: new[] { "ReserveHistoryId", "ReserveComponentId", "OrganisationId" });
                });

            migrationBuilder.InsertData(
                table: "CauseOfLossCodes",
                columns: new[] { "CauseOfLossCodeId", "Code", "CreatedAt", "IsActive", "Name", "OrganisationId", "PerilCategory", "SortOrder", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "COL-FIRE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Fire", new Guid("10000000-0000-0000-0000-000000000001"), "Property", 1, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "COL-FLOOD", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Flood", new Guid("10000000-0000-0000-0000-000000000001"), "Weather", 2, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "COL-THEFT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Theft", new Guid("10000000-0000-0000-0000-000000000001"), "Crime", 3, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "COL-VEH-COL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Vehicle Collision", new Guid("10000000-0000-0000-0000-000000000001"), "Auto", 4, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000005"), "COL-VEH-COMP", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Vehicle Comprehensive", new Guid("10000000-0000-0000-0000-000000000001"), "Auto", 5, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000006"), "COL-LIAB", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Third Party Liability", new Guid("10000000-0000-0000-0000-000000000001"), "Liability", 6, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000007"), "COL-EQUIP", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Equipment Breakdown", new Guid("10000000-0000-0000-0000-000000000001"), "Equipment", 7, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000008"), "COL-WIND", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Wind / Storm", new Guid("10000000-0000-0000-0000-000000000001"), "Weather", 8, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000009"), "COL-INJURY", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Bodily Injury", new Guid("10000000-0000-0000-0000-000000000001"), "Liability", 9, null, null, null },
                    { new Guid("40000000-0000-0000-0000-000000000010"), "COL-OTHER", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Other / Unknown", new Guid("10000000-0000-0000-0000-000000000001"), "General", 10, null, null, null }
                });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "ClaimStatusTransitionId", "CreatedAt", "From", "OrganisationId", "RequiredRole", "RuleCode", "To", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Draft", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-02", "Open", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Open", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-01", "UnderInvestigation", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Open", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-01", "PendingPayment", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Open", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-03", "Closed", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Open", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-01", "Withdrawn", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000006"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UnderInvestigation", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-02", "Open", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000007"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UnderInvestigation", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-01", "PendingPayment", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000008"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UnderInvestigation", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-03", "Closed", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000009"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UnderInvestigation", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-01", "Withdrawn", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000010"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PendingPayment", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-03", "Closed", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000011"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Closed", new Guid("10000000-0000-0000-0000-000000000001"), "Supervisor", "BR-ST-04", "Reopened", null, null, null },
                    { new Guid("50000000-0000-0000-0000-000000000012"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Reopened", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", "BR-ST-02", "Open", null, null, null }
                });

            migrationBuilder.InsertData(
                table: "Policies",
                columns: new[] { "PolicyId", "ClientName", "CoverageTypes", "CreatedAt", "EffectiveDate", "ExpirationDate", "OrganisationId", "PolicyNumber", "Status", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), "Meridian Transport LLC", "[\"Vehicle\",\"Cargo\"]", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2024, 1, 1), new DateOnly(2026, 12, 31), new Guid("10000000-0000-0000-0000-000000000001"), "POL-2024-001001", "Active", null, null, null },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "Harborview Properties Inc", "[\"Property\",\"Liability\"]", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2024, 6, 1), new DateOnly(2026, 5, 31), new Guid("10000000-0000-0000-0000-000000000001"), "POL-2024-001002", "Expired", null, null, null },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "Coastal Builders Group", "[\"Property\",\"Equipment\"]", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2025, 3, 1), new DateOnly(2027, 2, 28), new Guid("10000000-0000-0000-0000-000000000001"), "POL-2025-002001", "Active", null, null, null },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "Stanton Medical Group", "[\"Liability\",\"Vehicle\"]", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2025, 1, 1), new DateOnly(2026, 12, 31), new Guid("10000000-0000-0000-0000-000000000001"), "POL-2025-002002", "Active", null, null, null },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "Archived Corp", "[\"Property\"]", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2020, 1, 1), new DateOnly(2021, 12, 31), new Guid("10000000-0000-0000-0000-000000000001"), "POL-2023-000099", "Expired", null, null, null }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatedAt", "Name", "OrganisationId", "Role", "UpdatedAt", "UserCreated", "UserModified", "Username" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Claims Handler", new Guid("10000000-0000-0000-0000-000000000001"), "Handler", null, null, null, "handler" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Claims Supervisor", new Guid("10000000-0000-0000-0000-000000000001"), "Supervisor", null, null, null, "supervisor" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Claims Manager", new Guid("10000000-0000-0000-0000-000000000001"), "Manager", null, null, null, "manager" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CauseOfLossCodes_Code",
                table: "CauseOfLossCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAuditLog_ClaimId_CreatedAt",
                table: "ClaimAuditLog",
                columns: new[] { "ClaimId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAuditLog_ClaimId_OrganisationId",
                table: "ClaimAuditLog",
                columns: new[] { "ClaimId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDocuments_ClaimId_OrganisationId",
                table: "ClaimDocuments",
                columns: new[] { "ClaimId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimNumberSequences_OrganisationId_Year",
                table: "ClaimNumberSequences",
                columns: new[] { "OrganisationId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimParties_ClaimId_OrganisationId",
                table: "ClaimParties",
                columns: new[] { "ClaimId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimReserveComponents_ClaimId_Component",
                table: "ClaimReserveComponents",
                columns: new[] { "ClaimId", "Component" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimReserveComponents_ClaimId_OrganisationId",
                table: "ClaimReserveComponents",
                columns: new[] { "ClaimId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRiskObjects_ClaimId_OrganisationId",
                table: "ClaimRiskObjects",
                columns: new[] { "ClaimId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_AssignedHandlerId_OrganisationId",
                table: "Claims",
                columns: new[] { "AssignedHandlerId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_OrganisationId_ClaimNumber",
                table: "Claims",
                columns: new[] { "OrganisationId", "ClaimNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PolicyId_OrganisationId",
                table: "Claims",
                columns: new[] { "PolicyId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status",
                table: "Claims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_UpdatedAt",
                table: "Claims",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusTransitions_OrganisationId_From_To",
                table: "ClaimStatusTransitions",
                columns: new[] { "OrganisationId", "From", "To" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimValidationIssues_ClaimId_OrganisationId",
                table: "ClaimValidationIssues",
                columns: new[] { "ClaimId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_OrganisationId_UserId_Key",
                table: "IdempotencyRecords",
                columns: new[] { "OrganisationId", "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LossEvents_CauseOfLossCode_OrganisationId",
                table: "LossEvents",
                columns: new[] { "CauseOfLossCode", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LossEvents_ClaimId_OrganisationId",
                table: "LossEvents",
                columns: new[] { "ClaimId", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_PolicyNumber",
                table: "Policies",
                column: "PolicyNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveHistory_ClaimId_OrganisationId",
                table: "ReserveHistory",
                columns: new[] { "ClaimId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReserveHistory_IdempotencyKey",
                table: "ReserveHistory",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveHistory_ReserveComponentId_ChangeSequence",
                table: "ReserveHistory",
                columns: new[] { "ReserveComponentId", "ChangeSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveHistory_ReserveComponentId_ClaimId_OrganisationId",
                table: "ReserveHistory",
                columns: new[] { "ReserveComponentId", "ClaimId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReserveHistory_ResubmissionOfId_ReserveComponentId_OrganisationId",
                table: "ReserveHistory",
                columns: new[] { "ResubmissionOfId", "ReserveComponentId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganisationId_Username",
                table: "Users",
                columns: new[] { "OrganisationId", "Username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimAuditLog");

            migrationBuilder.DropTable(
                name: "ClaimDocuments");

            migrationBuilder.DropTable(
                name: "ClaimNumberSequences");

            migrationBuilder.DropTable(
                name: "ClaimParties");

            migrationBuilder.DropTable(
                name: "ClaimRiskObjects");

            migrationBuilder.DropTable(
                name: "ClaimStatusTransitions");

            migrationBuilder.DropTable(
                name: "ClaimValidationIssues");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "LossEvents");

            migrationBuilder.DropTable(
                name: "ReserveHistory");

            migrationBuilder.DropTable(
                name: "CauseOfLossCodes");

            migrationBuilder.DropTable(
                name: "ClaimReserveComponents");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
