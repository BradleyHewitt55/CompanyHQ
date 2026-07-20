namespace Abstractions.Companies;

public sealed record CompanyLocation(string ServerRoot, string CompanyName);

// Lets a tool work out which company it is "inside" from a directory alone, so the CLI
// can be run from a company directory without repeating --root and --c.
public interface ICompanyLocator
{
    // Walks up from the directory looking for a company folder (one whose parent is
    // the Companies root). Null when the directory sits outside any company.
    CompanyLocation? Locate(string directory);
}
