using Microsoft.EntityFrameworkCore;
using Services.Companies.Models;

namespace Services.Companies.Data;

public interface ICompanyProfileStore
{
    Task<CompanyProfile?> Get(CancellationToken cancellationToken = default);
    Task Save(CompanyProfile profile, CancellationToken cancellationToken = default);
}
