using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace ClaimsModule.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationReturnsTheServiceCollection()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddApplication().ShouldBeSameAs(services);
    }
}
