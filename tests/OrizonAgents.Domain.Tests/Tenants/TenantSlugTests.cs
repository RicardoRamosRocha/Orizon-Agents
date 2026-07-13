using OrizonAgents.Domain.Tenants;

namespace OrizonAgents.Domain.Tests.Tenants;

public class TenantSlugTests
{
    [Theory]
    [InlineData("Orizon Platform", "orizon-platform")]
    [InlineData("Açúcar & Café", "acucar-cafe")]
    [InlineData("  SaaS---Multi Tenant  ", "saas-multi-tenant")]
    public void Create_NormalizesSlug(string value, string expected)
    {
        string slug = TenantSlug.Create(value);

        Assert.Equal(expected, slug);
    }

    [Fact]
    public void Create_WithOnlyInvalidCharacters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TenantSlug.Create("!!!"));
    }

    [Fact]
    public void EnsureValid_WithUppercaseSlug_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TenantSlug.EnsureValid("Orizon"));
    }
}
