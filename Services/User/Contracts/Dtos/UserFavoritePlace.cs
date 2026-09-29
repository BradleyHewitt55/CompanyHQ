namespace CompanyHQ.Services.Users.Contracts.Dtos;

public sealed record OwnerUserFavoritePlaceDto(
    Guid Id,
    double Latitude,
    double Longitude,
    string Name,
    string Kind,
    string? Address,
    string? Phone);
