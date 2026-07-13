using System.Reflection;
using OrizonAgents.Application;

namespace OrizonAgents.Application.Tests;

public class ApplicationAssemblyTests
{
    [Fact]
    public void ApplicationAssembly_HasExpectedName()
    {
        Assembly assembly = typeof(AssemblyReference).Assembly;

        Assert.Equal("OrizonAgents.Application", assembly.GetName().Name);
    }

    [Fact]
    public void ApplicationAssembly_ReferencesDomainOnlyFromSolutionInnerLayers()
    {
        string[] referencedAssemblies = GetReferencedAssemblyNames(typeof(AssemblyReference).Assembly);

        Assert.Contains("OrizonAgents.Domain", referencedAssemblies);
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
