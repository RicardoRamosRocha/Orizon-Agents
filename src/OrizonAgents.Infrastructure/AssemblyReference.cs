namespace OrizonAgents.Infrastructure;

public sealed class AssemblyReference
{
    public static Type ApplicationAssembly => typeof(Application.AssemblyReference);

    public static Type DomainAssembly => typeof(Domain.AssemblyReference);
}
