using System.Text.Json;
using CompanyHQ.Company.Contracts.Models;
using CompanyHQ.Persistence.Services;
using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Company.Services;

internal sealed class CompanyServiceSelectionReader(IPersistence persistence) : IServiceSelectionReader
{
    public async Task<IReadOnlyDictionary<string, ServiceSelection>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var json = await persistence.Query<CompanyServiceState>().Where(state => state.Id == CompanyServiceState.SingletonId)
            .Select(state => state.SelectionsJson).FirstOrDefaultAsync(cancellationToken);
        return json is null ? new Dictionary<string, ServiceSelection>() : JsonSerializer.Deserialize<Dictionary<string, ServiceSelection>>(json)!;
    }
}
