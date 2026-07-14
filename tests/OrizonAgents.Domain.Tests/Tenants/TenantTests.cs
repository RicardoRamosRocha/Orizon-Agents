using OrizonAgents.Domain.Tenants;

namespace OrizonAgents.Domain.Tests.Tenants;

public class TenantTests
{
    [Fact]
    public void Create_WithValidName_CreatesActiveTenantWithDefaultSettings()
    {
        Tenant tenant = Tenant.Create("  Orizon Platform  ");

        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Equal("Orizon Platform", tenant.Name);
        Assert.Equal("orizon-platform", tenant.Slug);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.NotNull(tenant.Settings);
        Assert.Equal(tenant.Id, tenant.Settings.TenantId);
        Assert.Equal(TenantSettings.DefaultCulture, tenant.Settings.Culture);
        Assert.Equal(TenantSettings.DefaultTimeZone, tenant.Settings.TimeZone);
    }

    [Fact]
    public void Create_WithBlankName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Tenant.Create(" "));
    }

    [Fact]
    public void Rename_WithTooLongName_ThrowsArgumentException()
    {
        Tenant tenant = Tenant.Create("Orizon");
        string longName = new('a', Tenant.NameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => tenant.Rename(longName));
    }

    [Fact]
    public void ChangeStatus_WithInvalidStatus_ThrowsArgumentOutOfRangeException()
    {
        Tenant tenant = Tenant.Create("Orizon");

        Assert.Throws<ArgumentOutOfRangeException>(() => tenant.ChangeStatus((TenantStatus)999));
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Archived)]
    public void ChangeStatus_WithDefinedStatus_UpdatesTenantStatus(TenantStatus status)
    {
        Tenant tenant = Tenant.Create("Orizon");

        tenant.ChangeStatus(status);

        Assert.Equal(status, tenant.Status);
    }

    [Fact]
    public void Suspend_WithBlankReason_ThrowsArgumentException()
    {
        Tenant tenant = Tenant.Create("Orizon");

        Assert.Throws<ArgumentException>(() => tenant.Suspend(" ", DateTime.UtcNow));
    }

    [Fact]
    public void Suspend_WithUtcDate_StoresReasonAndSuspendedDate()
    {
        Tenant tenant = Tenant.Create("Orizon");
        DateTime utcNow = DateTime.UtcNow;

        tenant.Suspend("Contrato em revisão", utcNow);

        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Equal("Contrato em revisão", tenant.SuspensionReason);
        Assert.Equal(utcNow, tenant.SuspendedAtUtc);
    }

    [Fact]
    public void Reactivate_ClearsSuspensionData()
    {
        Tenant tenant = Tenant.Create("Orizon");
        tenant.Suspend("Contrato em revisão", DateTime.UtcNow);

        tenant.Reactivate(DateTime.UtcNow);

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Null(tenant.SuspensionReason);
        Assert.Null(tenant.SuspendedAtUtc);
    }

    [Fact]
    public void EnsureConcurrencyStamp_WithDifferentStamp_ThrowsInvalidOperationException()
    {
        Tenant tenant = Tenant.Create("Orizon");

        Assert.Throws<InvalidOperationException>(() => tenant.EnsureConcurrencyStamp("outro"));
    }
}
