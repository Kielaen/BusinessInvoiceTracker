using Invoice.Domain.Businesses;
using Microsoft.AspNetCore.Authorization;

namespace Invoice.Api.Authorization;

public sealed record BusinessRoleRequirement(BusinessRole MinimumRole)
    : IAuthorizationRequirement;
