using Abstractions.Companies;

namespace CompanyHQ.Services.Companies.Services;

// Deliberately independent of IServerPaths: the point is to discover the root from a
// path on disk, so binding it to the configured root would defeat the purpose.
public sealed class CompanyLocator : ICompanyLocator
{
    private const string CompaniesFolder = "Companies";

    public CompanyLocation? Locate(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var current = new DirectoryInfo(Path.GetFullPath(directory));
        while (current is not null)
        {
            // A company is any directory sitting directly inside a "Companies" folder,
            // which makes the server root its grandparent.
            var parent = current.Parent;
            if (parent is not null
                && string.Equals(parent.Name, CompaniesFolder, StringComparison.OrdinalIgnoreCase)
                && parent.Parent is not null)
            {
                return new CompanyLocation(parent.Parent.FullName, current.Name);
            }

            current = parent;
        }

        return null;
    }
}
