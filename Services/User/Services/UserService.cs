
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Dtos;
using CompanyHQ.Users.Contracts.Enums;
using CompanyHQ.Users.Contracts.Forms;
using CompanyHQ.Users.Contracts.Models;
using CompanyHQ.Users.Contracts.Ports;
using CompanyHQ.Users.Extensions;

namespace CompanyHQ.Users.Services;

/// <inheritdoc cref="IUserService" />
/// <remarks>
/// The profile write takes <see cref="IPersistence"/> because it and the optional media release
/// must commit together; every read goes through <see cref="IRepository{TModel}"/>.
/// </remarks>
public sealed class UserService(
    IRepository<User> users,
    IPersistence context,
    IProfileMediaLifecycle? profileMedia = null) : IUserService
{
    public async Task<PagedResult<ClientUserDto>?> ListAsync(
        ResponseAudience audience,
        PageRequest paging,
        SearchRequest? search,
        UserRole? role,
        UserSort sort,
        SortDirection direction,
        CancellationToken cancellationToken = default)
    {
        var administrator = audience is ResponseAudience.Admin;
        if (!string.IsNullOrWhiteSpace(search?.Keyword) && !administrator)
        {
            return null;
        }

        // A named field is matched as given; an unnamed keyword keeps the directory's own rule
        // (exact address, or any part of the name).
        var query = !administrator
            ? users.Query()
            : string.IsNullOrWhiteSpace(search?.Field)
                ? users.Query().FilterAdminList(search?.Keyword, role)
                : users.Query().FilterAdminList(role: role)
                    .Search(search.Field, search.Keyword, typeof(AdminUserDto), UserProjections.SearchAliases);

        var page = await (administrator ? query.OrderAdminList(sort, direction) : query.OrderPublicList(sort, direction))
            .ToPageAsync(paging.Skip, paging.PageSize, UserProjections.For(audience), cancellationToken);

        return new PagedResult<ClientUserDto>(page.Items, paging.Page, paging.PageSize, page.Total);
    }

    public async Task<PagedResult<ClientUserLookupDto>> LookupAsync(
        PageRequest paging,
        string? search,
        UserSort sort,
        SortDirection direction,
        CancellationToken cancellationToken = default)
    {
        var page = await users.Query()
            .FilterLookup(search)
            .OrderPublicList(sort, direction)
            .ToPageAsync(paging.Skip, paging.PageSize, UserProjections.ToLookup, cancellationToken);

        return new PagedResult<ClientUserLookupDto>(page.Items, paging.Page, paging.PageSize, page.Total);
    }

    public Task<ClientUserDto?> GetAsync(
        string slug,
        Guid? actingUserId,
        ResponseAudience audience,
        CancellationToken cancellationToken = default) =>
        Resolve(slug, actingUserId, audience is ResponseAudience.Admin)
            .OrderBy(user => user.CreatedAt)
            .Select(UserProjections.For(audience))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<OwnerUserDto?> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return context.InTransactionAsync(async _ =>
        {
            var user = await users.GetAsync(userId, cancellationToken);
            if (user is null)
            {
                return null;
            }

            var previousProfileImageUrl = user.ProfileImageUrl;
            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
            user.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl)
                ? null
                : request.ProfileImageUrl.Trim();
            await users.UpdateAsync(user, cancellationToken);

            if (profileMedia is not null)
            {
                await profileMedia.ReleaseUserProfileImageAsync(
                    user.Id,
                    previousProfileImageUrl,
                    user.ProfileImageUrl,
                    cancellationToken);
            }

            // The owner shape, not the client one: they are allowed their own identifier back.
            return new OwnerUserDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Bio,
                user.ProfileImageUrl,
                user.CreatedAt);
        }, cancellationToken: cancellationToken);
    }

    private IQuery<User> Resolve(string slug, Guid? actingUserId, bool administrator)
    {
        var identifier = slug.Trim();

        if (Guid.TryParse(identifier, out var id))
        {
            return administrator || actingUserId == id
                ? users.Query().Where(user => user.Id == id)
                : users.Query().Where(_ => false);
        }

        // The slug is the full name as shown, "First Last", matched case-insensitively.
        if (!administrator)
        {
            return users.Query().Where(user =>
                QueryFunctions.LikeIgnoreCase(((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(), identifier));
        }

        var email = identifier.ToLowerInvariant();
        return users.Query().Where(user =>
            user.Email == email
            || QueryFunctions.LikeIgnoreCase(((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(), identifier));
    }
}
