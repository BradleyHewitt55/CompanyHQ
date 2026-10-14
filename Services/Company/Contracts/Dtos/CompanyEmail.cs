using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Company.Contracts.Dtos;

/// <summary>A published company mailbox, independent of user accounts.</summary>
public sealed record ClientCompanyEmailDto(
    [Required, MaxLength(160)] string Title,
    [Required, EmailAddress, MaxLength(320)] string Address,
    [MaxLength(2_000)] string? Description = null);
