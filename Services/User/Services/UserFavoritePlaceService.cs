using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Users.Services;

public sealed class UserFavoritePlaceService(IRepository<UserFavoritePlace> places) : IUserFavoritePlaceService
{
    public async Task<IReadOnlyList<OwnerUserFavoritePlaceDto>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await places.Query()
            .Where(place => place.UserId == userId)
            .OrderBy(place => place.Name)
            .Select(place => new OwnerUserFavoritePlaceDto(place.Id, place.Latitude, place.Longitude, place.Name, place.Kind, place.Address, place.Phone))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OwnerUserFavoritePlaceDto>> ToggleAsync(
        Guid userId,
        ToggleFavoritePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var kind = request.Kind.Trim();
        var removed = await places.DeleteAsync(
            place => place.UserId == userId
                && place.Kind == kind
                && place.Latitude == request.Latitude
                && place.Longitude == request.Longitude
                && place.Name == name,
            cancellationToken);

        if (removed == 0)
        {
            await places.AddAsync(
                new UserFavoritePlace
                {
                    UserId = userId,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    Name = name,
                    Kind = kind,
                    Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                    Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim()
                },
                cancellationToken);
        }

        return await ListAsync(userId, cancellationToken);
    }
}
