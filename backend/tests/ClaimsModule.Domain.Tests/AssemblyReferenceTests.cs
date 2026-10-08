using Shouldly;

namespace ClaimsModule.Domain.Tests;

public sealed class AssemblyReferenceTests
{
    [Fact]
    public void AssemblyReferencePointsToDomainAssembly()
    {
        AssemblyReference.Assembly.GetName().Name.ShouldBe("ClaimsModule.Domain");
    }
}
