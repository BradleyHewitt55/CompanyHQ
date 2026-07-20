namespace Abstractions.Companies;

// Scoped ambient state, not a lookup: hosts set Current from the request's root
// domain, and the admin "switch company" service sets it directly. Everything
// downstream (data access included) reads it instead of taking a company parameter,
// so service contracts stay company-agnostic.
public interface ICompanyContext
{
    // Null means platform scope: shared CompanyHQ state that belongs to no company.
    string? Current { get; set; }
}
