using System.Security.Claims;
using Abstractions.Companies;
using Abstractions.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace CompanyHQ.Services.Auth.Services.Authorization;

public sealed class CurrentCompanyCookieEvents(
    ICompanyContext companyContext,
    IUserService users,
    ICurrentUser currentUser) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var claimedCompany = context.Principal?.FindFirst(AuthClaims.Company)?.Value;
        if (companyContext.Current is null
            || !string.Equals(claimedCompany, companyContext.Current, StringComparison.OrdinalIgnoreCase))
        {
            await RejectAsync(context);
            return;
        }

        // The cookie must not outlive the account: reloading the user here means a
        // deleted user's session dies on the next request, not at cookie expiry.
        var user = Guid.TryParse(
            context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)
            ? await users.GetAsync(userId, context.HttpContext.RequestAborted)
            : null;
        if (user is null)
        {
            await RejectAsync(context);
            return;
        }

        currentUser.User = user;
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return base.RedirectToLogin(context);
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return base.RedirectToAccessDenied(context);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        // Delete the dead cookie so the browser stops replaying it on every request.
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
