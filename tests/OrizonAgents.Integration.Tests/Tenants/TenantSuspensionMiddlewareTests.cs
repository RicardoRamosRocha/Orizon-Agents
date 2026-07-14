using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Tenants;
using OrizonAgents.Application.Tenants.Models;
using OrizonAgents.Application.Tenants.Requests;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Integration.Tests.Tenants;

public class TenantSuspensionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RedirectsAuthenticatedTenantUserWhenTenantIsSuspended()
    {
        Guid tenantId = Guid.NewGuid();
        var context = CreateContext("/Admin/Dashboard", tenantId);
        var middleware = new TenantSuspensionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, new StubTenantManagementService(isSuspended: true));

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/conta/organizacao-suspensa", context.Response.Headers.Location);
    }

    [Fact]
    public async Task InvokeAsync_AllowsLogoutForSuspendedTenant()
    {
        Guid tenantId = Guid.NewGuid();
        bool nextCalled = false;
        var context = CreateContext("/conta/sair", tenantId);
        var middleware = new TenantSuspensionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new StubTenantManagementService(isSuspended: true));

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext CreateContext(string path, Guid tenantId)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(OrizonClaimTypes.TenantId, tenantId.ToString()),
                new Claim(ClaimTypes.Role, OrizonRoles.TenantMember)
            ],
            authenticationType: "Test"));
        return context;
    }

    private sealed class StubTenantManagementService : ITenantManagementService
    {
        private readonly bool _isSuspended;

        public StubTenantManagementService(bool isSuspended)
        {
            _isSuspended = isSuspended;
        }

        public Task<bool> IsTenantSuspendedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_isSuspended);
        }

        public Task<PagedResult<TenantListItemDto>> ListAsync(TenantListRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TenantDetailsDto?> GetDetailsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TenantOrganizationDto?> GetOrganizationAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OperationResult<Guid>> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OperationResult> UpdateAsync(UpdateTenantRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OperationResult> SuspendAsync(SuspendTenantRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OperationResult> ReactivateAsync(ReactivateTenantRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OperationResult> UpdateOwnSettingsAsync(UpdateOwnTenantSettingsRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
