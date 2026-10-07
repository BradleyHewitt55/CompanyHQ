using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Data;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Ports;
using CompanyHQ.Services.Users.Extensions;

namespace CompanyHQ.Services.Users.Services;

/// <inheritdoc cref="IUserService" />
/// <remarks>
/// The profile write takes <see cref="PostgresDbContext"/> because it and the optional media release
/// must commit together; every read goes through <see cref="IPostgresService{TModel}"/>.
/// </remarks>
public sealed class UserService(
    IPostgresService<User> users,
    PostgresDbContext context,
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
                : users.Query().FilterAdminList(role: role).Search<User, AdminUserDto>(search, UserProjections.SearchAliases);

        return await query.ToPagedResultAsync(
            paging,
            UserProjections.For(audience),
            items => administrator ? items.OrderAdminList(sort, direction) : items.OrderPublicList(sort, direction),
            cancellationToken);
    }

    public Task<PagedResult<ClientUserLookupDto>> LookupAsync(
        PageRequest paging,
        string? search,
        UserSort sort,
        SortDirection direction,
        CancellationToken cancellationToken = default) =>
        users.Query()
            .FilterLookup(search)
            .ToPagedResultAsync(
                paging,
                UserProjections.ToLookup,
                items => items.OrderPublicList(sort, direction),
                cancellationToken);

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

        return context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

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

            await transaction.CommitAsync(cancellationToken);

            // The owner shape, not the client one: they are allowed their own identifier back.
            return new OwnerUserDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Bio,
                user.ProfileImageUrl,
                user.CreatedAt);
        });
    }

    private IQueryable<User> Resolve(string slug, Guid? actingUserId, bool administrator)
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
                EF.Functions.ILike(((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(), identifier));
        }

        var email = identifier.ToLowerInvariant();
        return users.Query().Where(user =>
            user.Email == email
            || EF.Functions.ILike(((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(), identifier));
    }
}
