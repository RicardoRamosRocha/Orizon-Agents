using OrizonAgents.Domain.Integrations;

namespace OrizonAgents.Domain.Tests.Integrations;

public sealed class IntegrationConnectionTests
{
    [Fact]
    public void Constructor_RequiresTenantAndKnownProvider()
    {
        Assert.Throws<ArgumentException>(() => new IntegrationConnection(Guid.Empty, "Conta", IntegrationProvider.Gmail));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntegrationConnection(Guid.NewGuid(), "Conta", (IntegrationProvider)999));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorAndRename_RejectEmptyNames(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new IntegrationConnection(Guid.NewGuid(), name!, IntegrationProvider.Gmail));
        var connection = new IntegrationConnection(Guid.NewGuid(), "Conta", IntegrationProvider.Gmail);
        Assert.ThrowsAny<ArgumentException>(() => connection.Rename(name!));
        Assert.Equal("Conta", connection.Name);
    }

    [Fact]
    public void NameLimit_AppliesToCreationAndRename()
    {
        string validName = new('a', IntegrationConnection.NameMaxLength);
        var connection = new IntegrationConnection(Guid.NewGuid(), validName, IntegrationProvider.Gmail);
        Assert.Throws<ArgumentException>(() => connection.Rename(validName + "a"));
        Assert.Throws<ArgumentException>(() => new IntegrationConnection(Guid.NewGuid(), validName + "a", IntegrationProvider.Gmail));
    }

    [Fact]
    public void ProtectedCredentials_RejectEmptyAndOversizedPayloads()
    {
        var connection = new IntegrationConnection(Guid.NewGuid(), "Conta", IntegrationProvider.Gmail);
        Assert.ThrowsAny<ArgumentException>(() => connection.ReplaceProtectedCredentials(" "));
        Assert.Throws<ArgumentException>(() => connection.ReplaceProtectedCredentials(new string('a', IntegrationConnection.EncryptedCredentialsMaxLength + 1)));
        Assert.Null(connection.EncryptedCredentials);
    }
}