using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Users.Contracts.Forms;

public sealed record ToggleFavoritePlaceRequest(
    [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude,
    [Required, MinLength(1), MaxLength(200)] string Name,
    [Required, MinLength(1), MaxLength(64)] string Kind,
    [MaxLength(500)] string? Address = null,
    [MaxLength(50)] string? Phone = null);
