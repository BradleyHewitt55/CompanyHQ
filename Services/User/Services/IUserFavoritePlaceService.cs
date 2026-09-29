using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Forms;

namespace CompanyHQ.Services.Users.Services;

public interface IUserFavoritePlaceService
{
    Task<IReadOnlyList<OwnerUserFavoritePlaceDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OwnerUserFavoritePlaceDto>> ToggleAsync(Guid userId, ToggleFavoritePlaceRequest request, CancellationToken cancellationToken = default);
}
