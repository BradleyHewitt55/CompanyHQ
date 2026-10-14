using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Shipping.Contracts.Dtos;

/// <summary>
/// A street address, with coordinates when the caller already has them. It is both what a caller
/// sends to be quoted and what staff read back for a fulfilment origin.
/// </summary>
public sealed record ShippingAddressDto(
    [MaxLength(255)] string? StreetAddress = null,
    [MaxLength(100)] string? ApartmentUnit = null,
    [MaxLength(150)] string? Suburb = null,
    [MaxLength(150)] string? City = null,
    [MaxLength(150)] string? Province = null,
    [MaxLength(32)] string? PostalCode = null,
    [MaxLength(100)] string? Country = null,
    [Range(-90, 90)] double? Latitude = null,
    [Range(-180, 180)] double? Longitude = null);
