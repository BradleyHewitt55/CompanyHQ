using Abstractions.Companies;

namespace Admin.Infrastructure;

public sealed class CompanyResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        ICompanyRegistry registry,
        ICompanyContext companyContext)
    {
        var configured = configuration[$"CompanyHq:Domains:{httpContext.Request.Host.Host}"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            companyContext.Current = configured;
        }
        else
        {
            var companies = await registry.ListAsync(httpContext.RequestAborted);
            if (companies.Count == 1)
            {
                companyContext.Current = companies[0];
            }
        }

        await next(httpContext);
    }
}
