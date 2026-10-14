using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Users.Contracts.Models;

public sealed class UserFavoritePlace : BaseModel
{
    public Guid UserId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public required string Name { get; set; }

    /// <summary>What the place is, as the place search reported it (an OpenStreetMap amenity value).</summary>
    public required string Kind { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }
}
