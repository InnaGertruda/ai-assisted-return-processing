using ReturnFlow.Application;
using ReturnFlow.Domain;
using ReturnFlow.Infrastructure;
using Xunit;

namespace ReturnFlow.UnitTests;

public sealed class ModuleBoundaryTests
{
    [Theory]
    [InlineData(typeof(DomainAssembly), "ReturnFlow.Domain")]
    [InlineData(typeof(ApplicationAssembly), "ReturnFlow.Application")]
    [InlineData(typeof(InfrastructureAssembly), "ReturnFlow.Infrastructure")]
    public void Module_marker_is_in_expected_assembly(Type marker, string expectedAssemblyName)
    {
        Assert.Equal(expectedAssemblyName, marker.Assembly.GetName().Name);
    }
}
