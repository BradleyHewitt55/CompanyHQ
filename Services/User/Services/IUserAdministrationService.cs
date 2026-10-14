using CompanyHQ.Users.Contracts.Dtos;
using CompanyHQ.Users.Contracts.Forms;

namespace CompanyHQ.Users.Services;

/// <summary>Current-database-admin account mutations shared by API and Web.</summary>
public interface IUserAdministrationService
{
    Task<UserAdministrationOutcome> CreateAsync(
        Guid actingAdministratorId,
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserAdministrationOutcome> UpdateAsync(
        Guid actingAdministratorId,
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);
}
