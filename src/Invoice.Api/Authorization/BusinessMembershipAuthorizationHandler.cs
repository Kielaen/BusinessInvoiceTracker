using System.Security.Claims;
using Invoice.Domain.Businesses;
using Microsoft.AspNetCore.Authorization;

namespace Invoice.Api.Authorization;

public sealed class BusinessMembershipAuthorizationHandler
    : AuthorizationHandler<BusinessRoleRequirement, BusinessMembership>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BusinessRoleRequirement requirement,
        BusinessMembership resource)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(subject, out var userId) &&
            resource.UserId == userId &&
            resource.IsActive &&
            resource.Role >= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
