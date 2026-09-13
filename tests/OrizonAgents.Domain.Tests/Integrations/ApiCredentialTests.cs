using OrizonAgents.Domain.Integrations;

namespace OrizonAgents.Domain.Tests.Integrations;

public class ApiCredentialTests
{
    [Fact]
    public void Constructor_RequiresAgentAndKeepsOnlyKeyMetadata()
    {
        Guid tenantId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();

        var credential = new ApiCredential(
            tenantId,
            agentId,
            " Produção ",
            "identifier",
            "ABC123");

        Assert.Equal(tenantId, credential.TenantId);
        Assert.Equal(agentId, credential.AgentId);
        Assert.Equal("Produção", credential.Name);
        Assert.Equal("identifier", credential.KeyIdentifier);
        Assert.Equal("ABC123", credential.KeyHash);
        Assert.True(credential.IsActive);
        Assert.Null(credential.RevokedAtUtc);
    }

    [Fact]
    public void Revoke_DeactivatesCredentialAndKeepsFirstRevocationDate()
    {
        var credential = new ApiCredential(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Produção",
            "identifier",
            "ABC123");
        var firstRevocation = new DateTime(2026, 8, 31, 20, 0, 0, DateTimeKind.Utc);

        credential.Revoke(firstRevocation);
        credential.Revoke(firstRevocation.AddMinutes(1));

        Assert.False(credential.IsActive);
        Assert.Equal(firstRevocation, credential.RevokedAtUtc);
    }

    [Fact]
    public void Revoke_RejectsNonUtcDate()
    {
        var credential = new ApiCredential(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Produção",
            "identifier",
            "ABC123");

        Assert.Throws<ArgumentException>(() =>
            credential.Revoke(DateTime.Now));
    }
}
