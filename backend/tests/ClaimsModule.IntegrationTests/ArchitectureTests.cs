using System.Xml.Linq;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class ArchitectureTests
{
    public static TheoryData<string, string[]> AllowedProjectReferences => new()
    {
        { "ClaimsModule.Domain", [] },
        { "ClaimsModule.Application", ["ClaimsModule.Domain"] },
        { "ClaimsModule.Infrastructure", ["ClaimsModule.Application"] },
        { "ClaimsModule.Persistence", ["ClaimsModule.Application"] },
        {
            "ClaimsModule.API",
            [
                "ClaimsModule.Application",
                "ClaimsModule.Domain",
                "ClaimsModule.Infrastructure",
                "ClaimsModule.Persistence",
            ]
        },
    };

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void ProductionProjectsOnlyReferenceAllowedProjects(
        string assemblyName,
        string[] allowedReferences)
    {
        // AGENTS.md defines DIRECT project references. Domain types remain available transitively to Persistence.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "backend", "ClaimsModule.sln"))) root = root.Parent;
        root.ShouldNotBeNull();
        var project = XDocument.Load(Path.Combine(root.FullName, "backend", "src", assemblyName, assemblyName + ".csproj"));
        var actualReferences = project.Descendants("ProjectReference").Select(x =>
            Path.GetFileNameWithoutExtension(x.Attribute("Include")!.Value.Replace('\\', '/'))).ToArray();

        actualReferences.Except(allowedReferences).ShouldBeEmpty();
    }

    [Fact]
    public void ProductionAuditWritesAreCentralizedInAuditLogService()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "backend", "ClaimsModule.sln"))) root = root.Parent;
        root.ShouldNotBeNull();
        var sourceRoot = Path.Combine(root.FullName, "backend", "src");
        var directWriters = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("new ClaimAuditLog", StringComparison.Ordinal)
                    || source.Contains("ClaimAuditLog.Add", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(sourceRoot, path).Replace('\\', '/')).ToArray();

        directWriters.ShouldBe(["ClaimsModule.Persistence/Services/PersistenceServices.cs"]);
        Directory.EnumerateFiles(Path.Combine(sourceRoot, "ClaimsModule.API"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ShouldAllBe(source => !source.Contains("ClaimAuditLog", StringComparison.Ordinal));
    }
}
