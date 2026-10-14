namespace CompanyHQ.Users.Contracts.Dtos;

/// <summary>Minimal account identity for authenticated co-owner selection.</summary>
public sealed record ClientUserLookupDto(Guid Id, string? FirstName, string? LastName);
