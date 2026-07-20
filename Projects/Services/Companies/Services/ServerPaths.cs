using Abstractions.Companies;
using Microsoft.Extensions.Configuration;

namespace CompanyHQ.Services.Companies.Services;

public sealed class ServerPaths : IServerPaths
{
    public ServerPaths(IConfiguration configuration)
    {
        // Everything CompanyHQ owns lives in one directory in the server user's home
        // (~/CompanyHQ), so backup/inspection is a single tree. Development points
        // CompanyHq:ServerRoot at MockServer/ in the repo to exercise the identical
        // layout without touching the real one.
        var root = configuration["CompanyHq:ServerRoot"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CompanyHQ");
        CompaniesRoot = Path.Combine(Path.GetFullPath(root), "Companies");
    }

    public string CompaniesRoot { get; }

    public string CompanyDirectory(string companyName)
        => Path.Combine(CompaniesRoot, companyName);
}
