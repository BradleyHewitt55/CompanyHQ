namespace Abstractions.Companies;

public interface ICompanyRegistry
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    // Registration creates the company and Services directories. Database setup is
    // orchestrated by CompanyCreation; the optional string remains for scripted hosts.
    Task RegisterAsync(
        string companyName,
        string? externalConnectionString = null,
        CancellationToken cancellationToken = default);
}
