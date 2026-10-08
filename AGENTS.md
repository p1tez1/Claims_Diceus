# Agent instructions

## Project summary and source of truth

Build a greenfield Claims Module covering FNOL intake, claim lifecycle, reserve management, GL simulation, documents, and audit logging. Use .NET 9 / C# 13, EF Core 9, Clean Architecture, SQL Server 2022 or Azure SQL, Azure Blob Storage, Hangfire, and Angular 18+.

Read D1 (`docs/spec/spec-1-functional.md`) for functional behavior and D2 (`docs/spec/spec-2-assessment.md`) for stack, structure, deliverables, and deployment. Apply D1 over D2 for functional conflicts; apply D2 over D1 for technology, structure, and deliverable conflicts. Apply `docs/DESIGN-DECISIONS.md` over both only where it records an explicit decision. Preserve unresolved questions; do not treat them as decisions.

## Repository layout

Use this target layout; inspect which parts actually exist before working:

- `/backend/ClaimsModule.sln`
- `/backend/src/ClaimsModule.Domain`, `ClaimsModule.Application`, `ClaimsModule.Infrastructure`, `ClaimsModule.Persistence`, and `ClaimsModule.API`
- `/backend/tests/*`
- `/frontend`
- `/docs` (specifications and design documentation)
- `/infra`
- `/.github/workflows`

## Workflow: every session

1. Read this file and `docs/DESIGN-DECISIONS.md`; read the relevant specification sections.
2. Inspect architecture, relevant files, existing patterns, related tests, dependencies, and similar functionality.
3. Trace the existing flow from entry point through domain, persistence, integrations, and tests. If no implementation exists, state that.
4. Finish inspection before editing. Do not assume what the repository can answer.
5. Search before creating abstractions. Reuse existing patterns and implementations.

## Constraints

Keep changes focused. Make no unrelated edits, do not rewrite working code, and do not break existing APIs. Add no dependencies unless the task prompt explicitly lists them. Respect the package policy when choosing an authorized dependency. Explain conflicts between requirements and architecture before making a major architectural change. If an unresolved ambiguity materially affects behavior, STOP and report it instead of inventing behavior.

## Dependency rules

Treat arrows as direct project references:

- Domain: reference no other project or external package.
- Application -> Domain.
- Persistence -> Application.
- Infrastructure -> Application.
- API -> Domain, Application, Persistence, Infrastructure.

Keep external implementations behind Application interfaces. Follow D2's project responsibilities.

## Package policy

Pin authorized packages to compatible versions. Use MediatR 12.x, AutoMapper <=14.x, the latest stable FluentValidation compatible with the required stack, and Serilog. Use xUnit + Shouldly + NSubstitute for tests; choose NSubstitute to provide test doubles without Moq. Do not use FluentAssertions >=8 or any commercially licensed major. Verify version and license suitability when selecting packages; record unresolved conflicts rather than changing this policy silently.

## Verification

Run `dotnet build backend/ClaimsModule.sln` and `dotnet test` for the relevant test projects when backend code changes. Run the frontend build and relevant tests when frontend files change, using the project's existing scripts. Fix failures before reporting completion. If environment constraints prevent verification, report the exact blocker and mark checks unverified. For documentation-only tasks with no testing required, review the documents and scope; do not manufacture missing projects to run checks.

## Final report

Report these eight items, including explicit not-applicable or blocked results:

1. What was implemented.
2. Files changed.
3. Architectural decisions.
4. Tests executed.
5. Test results.
6. Build result.
7. Assumptions.
8. Remaining issues.

