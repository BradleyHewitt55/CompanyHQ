namespace Abstractions.Companies;

// The server root is swappable so the same code runs against Companies/ in
// production and MockServer/ during development — the layout underneath is identical.
public interface IServerPaths
{
    string CompaniesRoot { get; }

    string CompanyDirectory(string companyName);
}
