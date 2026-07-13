using System.Reflection;
using OrizonAgents.Infrastructure;

namespace OrizonAgents.Integration.Tests;

public class InfrastructureAssemblyTests
{
    [Fact]
    public void InfrastructureAssembly_HasExpectedName()
    {
        Assembly assembly = typeof(AssemblyReference).Assembly;

        Assert.Equal("OrizonAgents.Infrastructure", assembly.GetName().Name);
    }

    [Fact]
    public void InfrastructureAssembly_ReferencesApplicationAndDomain()
    {
        string[] referencedAssemblies = GetReferencedAssemblyNames(typeof(AssemblyReference).Assembly);

        Assert.Contains("OrizonAgents.Application", referencedAssemblies);
        Assert.Contains("OrizonAgents.Domain", referencedAssemblies);
        Assert.DoesNotContain("OrizonAgents.API", referencedAssemblies);
        Assert.DoesNotContain("OrizonAgents.Web", referencedAssemblies);
        Assert.DoesNotContain("OrizonAgents.Workers", referencedAssemblies);
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .ToArray();
    }
}
