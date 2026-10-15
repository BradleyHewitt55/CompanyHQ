using CompanyHQ.Company.Contracts.Ports;
using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Users.Services;

namespace CompanyHQ.Company.Services;

internal sealed class CompanyServiceNavigation(IServiceAvailability availability, IUserAuthorizationService authorization)
    : ICompanyServiceNavigation
{
    public async Task<IReadOnlyList<ServiceNavigationDto>> ListAsync(Guid actingUserId, CancellationToken cancellationToken = default)
    {
        if (actingUserId == Guid.Empty) throw new UnauthorizedAccessException();
        var entries = new List<ServiceNavigationDto>();
        foreach (var service in await availability.ListAsync(cancellationToken))
        {
            var descriptor = service.Descriptor;
            if (!service.MenuVisible || descriptor.MenuPath is null || descriptor.ReadPermission is null) continue;
            var permissions = new[] { descriptor.ReadPermission }.Concat(descriptor.AdditionalReadPermissions ?? []);
            foreach (var permission in permissions)
            {
                if (!await authorization.HasPermissionAsync(actingUserId, permission, cancellationToken)) continue;
                entries.Add(new(descriptor.Key, descriptor.Name, descriptor.MenuPath, service.Status));
                break;
            }
        }
        return entries;
    }
}
