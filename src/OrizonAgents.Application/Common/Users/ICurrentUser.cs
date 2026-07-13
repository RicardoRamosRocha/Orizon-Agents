namespace OrizonAgents.Application.Common.Users;

public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
