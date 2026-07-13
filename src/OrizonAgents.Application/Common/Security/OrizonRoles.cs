namespace OrizonAgents.Application.Common.Security;

public static class OrizonRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string TenantMember = "TenantMember";

    public static readonly string[] All =
    [
        PlatformAdmin,
        TenantAdmin,
        TenantMember
    ];
}
