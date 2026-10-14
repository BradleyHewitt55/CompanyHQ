using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Users.Contracts.Dtos;
using CompanyHQ.Users.Contracts.Enums;
using CompanyHQ.Users.Contracts.Forms;

namespace CompanyHQ.Users.Services;

/// <summary>Account reads and the one self-service profile write.</summary>
public interface IUserService
{
    /// <summary>
    /// One page of accounts in <paramref name="audience"/>'s shape. Null when a non-administrator
    /// supplies <paramref name="search"/>: only an operator may search the directory by text. With
    /// no field named the keyword matches the address exactly or any part of the name.
    /// </summary>
    Task<PagedResult<ClientUserDto>?> ListAsync(
        ResponseAudience audience,
        PageRequest paging,
        SearchRequest? search,
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
