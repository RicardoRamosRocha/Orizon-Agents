using System.Reflection;
using OrizonAgents.Domain;

namespace OrizonAgents.Domain.Tests;

public class DomainAssemblyTests
{
    [Fact]
    public void DomainAssembly_HasExpectedName()
    {
        Assembly assembly = typeof(AssemblyReference).Assembly;

        Assert.Equal("OrizonAgents.Domain", assembly.GetName().Name);
    }

    [Fact]
    public void DomainAssembly_DoesNotReferenceOuterLayers()
    {
        string[] referencedAssemblies = GetReferencedAssemblyNames(typeof(AssemblyReference).Assembly);

        Assert.DoesNotContain("OrizonAgents.Application", referencedAssemblies);
        Assert.DoesNotContain("OrizonAgents.Infrastructure", referencedAssemblies);
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
