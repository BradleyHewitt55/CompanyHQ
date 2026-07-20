using Abstractions.Companies;
using Microsoft.AspNetCore.Authorization;

namespace CompanyHQ.Services.Auth.Services.Authorization;

public sealed class CurrentCompanyRequirement : IAuthorizationRequirement;

public sealed class CurrentCompanyHandler(ICompanyContext companyContext)
    : AuthorizationHandler<CurrentCompanyRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentCompanyRequirement requirement)
    {
        var claimedCompany = context.User.FindFirst(AuthClaims.Company)?.Value;
        if (companyContext.Current is not null
            && string.Equals(claimedCompany, companyContext.Current, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
