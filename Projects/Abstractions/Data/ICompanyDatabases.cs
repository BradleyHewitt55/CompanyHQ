namespace Abstractions.Data;

// Implemented by the loaded database provider plugin. The registry calls this so
// company registration works against whatever store is loaded, per the provider-swap rule.
public interface ICompanyDatabases
{
    // False until the user has made the hosted-vs-external choice for the company;
    // hosts use this to gate the dashboard behind database setup.
    bool IsConfigured(string companyName);

    // Idempotent: provisions the company's database (or records the external one),
    // persists its credentials under the company directory, and syncs the schema.
    Task EnsureDatabaseAsync(
        string companyName,
        string? externalConnectionString = null,
        CancellationToken cancellationToken = default);
}
