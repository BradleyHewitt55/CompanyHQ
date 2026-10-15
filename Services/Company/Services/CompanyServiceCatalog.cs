using System.Data;
using System.Text.Json;
using CompanyHQ.Company.Contracts.Dtos;
using CompanyHQ.Company.Contracts.Forms;
using CompanyHQ.Company.Contracts.Models;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Users.Services;

namespace CompanyHQ.Company.Services;

internal sealed class CompanyServiceCatalog(IPersistence persistence, IUserAuthorizationService authorization,
    IServiceAvailability availability, ServiceCatalogRegistry registry) : ICompanyServiceCatalog
{
    private async Task Authorize(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } actor || !await authorization.HasPermissionAsync(actor, AccessPermissions.ManageServices, cancellationToken))
            throw new UnauthorizedAccessException();
    }

    public async Task<IReadOnlyList<ServiceCatalogDto>> ListAsync(Guid? actingUserId, CancellationToken cancellationToken = default)
    { await Authorize(actingUserId, cancellationToken); return await availability.ListAsync(cancellationToken); }

    public async Task<ServiceCatalogDto?> GetAsync(string key, Guid? actingUserId, CancellationToken cancellationToken = default)
    { await Authorize(actingUserId, cancellationToken); return await availability.GetAsync(key, cancellationToken); }

    public async Task<ServiceChangeResult> ChangeAsync(string key, ChangeServiceRequest request, Guid? actingUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await Authorize(actingUserId, cancellationToken);
        if (!registry.Services.TryGetValue(key, out var registration)) return new(null, ServiceChangeError.NotFound);
        ServiceChangeError error;
        try
        {
            error = await persistence.InTransactionAsync(async token =>
            {
                // Read the singleton under lock; SERIALIZABLE also protects its initial creation.
                var state = await persistence.Locked<CompanyServiceState>().Where(item => item.Id == CompanyServiceState.SingletonId).FirstOrDefaultAsync(token);
                var selections = state is null ? new Dictionary<string, ServiceSelection>()
                    : JsonSerializer.Deserialize<Dictionary<string, ServiceSelection>>(state.SelectionsJson)!;
                bool Enabled(ServiceDescriptor descriptor) => descriptor.Core ||
                    (selections.TryGetValue(descriptor.Key, out var selected) ? selected.Enabled : descriptor.DefaultEnabled);
                var descriptor = registration.Descriptor;
                if (descriptor.Core && !request.Enabled) return ServiceChangeError.CoreService;
                var provider = request.SelectedProvider ?? (selections.TryGetValue(key, out var current) ? current.SelectedProvider : descriptor.DefaultProvider);
                if (provider is not null && !(descriptor.Providers ?? []).Any(item => item.Key == provider)) return ServiceChangeError.UnknownProvider;
                var former = selections.TryGetValue(key, out var previous) ? previous.SelectedProvider : descriptor.DefaultProvider;
                if (descriptor.ProviderChangeRequiresMigration && provider != former) return ServiceChangeError.UnsafeProviderChange;
                if (request.Enabled && (descriptor.Dependencies ?? []).Any(dependency => !Enabled(registry.Services[dependency].Descriptor)))
                    return ServiceChangeError.DependencyUnavailable;
                if (!request.Enabled && registry.Services.Values.Any(item => Enabled(item.Descriptor) && (item.Descriptor.Dependencies ?? []).Contains(key)))
                    return ServiceChangeError.RequiredByEnabledService;
                selections[key] = new(request.Enabled, provider);
                if (state is null) { state = new() { Id = CompanyServiceState.SingletonId }; persistence.Add(state); }
                state.SelectionsJson = JsonSerializer.Serialize(selections);
                await persistence.SaveChangesAsync(token);
                return ServiceChangeError.None;
            }, IsolationLevel.Serializable, cancellationToken);
        }
        catch (UniqueConstraintException) { return new(null, ServiceChangeError.ConcurrentChange, "Another catalog change committed. Reload and retry."); }
        return new(await availability.GetAsync(key, cancellationToken), error,
            error == ServiceChangeError.UnsafeProviderChange ? "Media objects lack provider affinity. Migrate storage before changing its provider." : null);
    }
}
