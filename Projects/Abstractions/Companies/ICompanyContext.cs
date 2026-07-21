namespace Abstractions.Companies;

// Scoped ambient state, not a lookup: each host process seeds Current from its
// configured company. Everything downstream reads it instead of taking a company
// parameter, so service contracts stay company-agnostic.
public interface ICompanyContext
{
    // Null means platform scope: shared CompanyHQ state that belongs to no company.
    string? Current { get; set; }
}
