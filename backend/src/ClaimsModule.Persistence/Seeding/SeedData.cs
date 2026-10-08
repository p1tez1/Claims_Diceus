using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Rules;
using ClaimsModule.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Seeding;

internal static class SeedData
{
    internal static void Configure(ModelBuilder model)
    {
        var policies = new[]
        {
            new { Number = "POL-2024-001001", Client = "Meridian Transport LLC", Start = new DateOnly(2024, 1, 1), End = new DateOnly(2026, 12, 31), Status = PolicyStatus.Active, Coverage = new[] { "Vehicle", "Cargo" } },
            new { Number = "POL-2024-001002", Client = "Harborview Properties Inc", Start = new DateOnly(2024, 6, 1), End = new DateOnly(2026, 5, 31), Status = PolicyStatus.Expired, Coverage = new[] { "Property", "Liability" } },
            new { Number = "POL-2025-002001", Client = "Coastal Builders Group", Start = new DateOnly(2025, 3, 1), End = new DateOnly(2027, 2, 28), Status = PolicyStatus.Active, Coverage = new[] { "Property", "Equipment" } },
            new { Number = "POL-2025-002002", Client = "Stanton Medical Group", Start = new DateOnly(2025, 1, 1), End = new DateOnly(2026, 12, 31), Status = PolicyStatus.Active, Coverage = new[] { "Liability", "Vehicle" } },
            new { Number = "POL-2023-000099", Client = "Archived Corp", Start = new DateOnly(2020, 1, 1), End = new DateOnly(2021, 12, 31), Status = PolicyStatus.Expired, Coverage = new[] { "Property" } },
        };
        for (var i = 0; i < policies.Length; i++)
        {
            var p = policies[i];
            model.Entity<Policy>().HasData(new
            {
                Id = SeedConstants.PolicyIds[i],
                PolicyNumber = p.Number,
                ClientName = p.Client,
                EffectiveDate = p.Start,
                ExpirationDate = p.End,
                p.Status,
                CoverageTypes = p.Coverage,
                SeedConstants.OrganisationId,
                SeedConstants.CreatedAt
            });
        }
        (string Code, string Name, string Category)[] causes = [
            ("COL-FIRE", "Fire", "Property"), ("COL-FLOOD", "Flood", "Weather"), ("COL-THEFT", "Theft", "Crime"),
            ("COL-VEH-COL", "Vehicle Collision", "Auto"), ("COL-VEH-COMP", "Vehicle Comprehensive", "Auto"),
            ("COL-LIAB", "Third Party Liability", "Liability"), ("COL-EQUIP", "Equipment Breakdown", "Equipment"),
            ("COL-WIND", "Wind / Storm", "Weather"), ("COL-INJURY", "Bodily Injury", "Liability"),
            ("COL-OTHER", "Other / Unknown", "General"),
        ];
        for (var i = 0; i < causes.Length; i++)
            model.Entity<CauseOfLossCode>().HasData(new
            {
                Id = SeedConstants.CauseIds[i],
                causes[i].Code,
                causes[i].Name,
                PerilCategory = causes[i].Category,
                IsActive = true,
                SortOrder = i + 1,
                SeedConstants.OrganisationId,
                SeedConstants.CreatedAt
            });

        for (var i = 0; i < ClaimStateMachine.DefaultTransitions.Count; i++)
        {
            var edge = ClaimStateMachine.DefaultTransitions[i];
            model.Entity<ClaimStatusTransitionRow>().HasData(new
            {
                Id = SeedConstants.TransitionIds[i],
                edge.From,
                edge.To,
                edge.RequiredRole,
                edge.RuleCode,
                SeedConstants.OrganisationId,
                SeedConstants.CreatedAt
            });
        }
        model.Entity<User>().HasData(
            new { Id = SeedConstants.HandlerId, Username = "handler", Name = "Claims Handler", Role = UserRole.Handler, SeedConstants.OrganisationId, SeedConstants.CreatedAt },
            new { Id = SeedConstants.SupervisorId, Username = "supervisor", Name = "Claims Supervisor", Role = UserRole.Supervisor, SeedConstants.OrganisationId, SeedConstants.CreatedAt },
            new { Id = SeedConstants.ManagerId, Username = "manager", Name = "Claims Manager", Role = UserRole.Manager, SeedConstants.OrganisationId, SeedConstants.CreatedAt });
    }
}
