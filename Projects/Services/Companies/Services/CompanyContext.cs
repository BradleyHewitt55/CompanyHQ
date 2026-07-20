using Abstractions.Companies;

namespace CompanyHQ.Services.Companies.Services;

public sealed class CompanyContext : ICompanyContext
{
    public string? Current { get; set; }
}
