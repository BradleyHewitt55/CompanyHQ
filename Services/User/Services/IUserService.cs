using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Forms;

namespace CompanyHQ.Services.Users.Services;

/// <summary>Account reads and the one self-service profile write.</summary>
public interface IUserService
{
    /// <summary>
    /// One page of accounts in <paramref name="audience"/>'s shape. Null when a non-administrator
    /// supplies <paramref name="search"/>: only an operator may search the directory by text.
    /// </summary>
    Task<PagedResult<ClientUserDto>?> ListAsync(
        ResponseAudience audience,
        PageRequest paging,
        string? search,
        UserRole? role,
        UserSort sort,
        SortDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>Id-and-name lookup for picking another account, e.g. to start a conversation.</summary>
    Task<PagedResult<ClientUserLookupDto>> LookupAsync(
        PageRequest paging,
        string? search,
        UserSort sort,
        SortDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves one account by id, full name, or (for an administrator) email. An id resolves only for
    /// an administrator or the account itself, so the public route cannot enumerate identifiers.
    /// </summary>
    Task<ClientUserDto?> GetAsync(
        string slug,
        Guid? actingUserId,
        ResponseAudience audience,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces the caller's own profile. Null when the account no longer exists.</summary>
    Task<OwnerUserDto?> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
}
