
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Users.Contracts.Enums;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Users.Extensions;

/// <summary>Typed filtering and ordering for public and administrative user lists.</summary>
public static class UserListQueryExtensions
{
    public static IQuery<User> FilterLookup(
        this IQuery<User> query,
        string? search = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A term-less lookup would page the whole account table; the picker always has a term.
        if (string.IsNullOrWhiteSpace(search))
        {
            return query.Where(_ => false);
        }

        var term = search.Trim();
        // Equality, not ILIKE: a signed-in caller must not enumerate addresses by fragment.
        var email = term.ToLowerInvariant();
        var pattern = $"%{term}%";

        return query.Where(user =>
            user.Email == email
            || (user.FirstName != null && QueryFunctions.LikeIgnoreCase(user.FirstName, pattern))
            || (user.LastName != null && QueryFunctions.LikeIgnoreCase(user.LastName, pattern))
            || QueryFunctions.LikeIgnoreCase((user.FirstName ?? "") + " " + (user.LastName ?? ""), pattern));
    }

    public static IQuery<User> FilterAdminList(
        this IQuery<User> query,
        string? search = null,
        UserRole? role = null)
    {
        // A term-less lookup pages nothing, but a term-less admin list still browses everything.
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.FilterLookup(search);
        }

        return role is { } requestedRole
            ? query.Where(user => user.Role == requestedRole)
            : query;
    }

    public static IOrderedQuery<User> OrderPublicList(
        this IQuery<User> query,
        UserSort sort = UserSort.CreatedAt,
        SortDirection direction = SortDirection.Descending)
    {
        ArgumentNullException.ThrowIfNull(query);
        var descending = direction is not SortDirection.Ascending;

        return sort is UserSort.Name
            ? descending
                ? query.OrderByDescending(user => user.FirstName).ThenByDescending(user => user.LastName).ThenBy(user => user.Id)
                : query.OrderBy(user => user.FirstName).ThenBy(user => user.LastName).ThenBy(user => user.Id)
            : descending
                ? query.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
                : query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id);
    }

    public static IOrderedQuery<User> OrderAdminList(
        this IQuery<User> query,
        UserSort sort = UserSort.CreatedAt,
        SortDirection direction = SortDirection.Descending)
    {
        ArgumentNullException.ThrowIfNull(query);

        var descending = direction is not SortDirection.Ascending;

        return sort switch
        {
            UserSort.Email => descending
                ? query.OrderByDescending(user => user.Email).ThenBy(user => user.Id)
                : query.OrderBy(user => user.Email).ThenBy(user => user.Id),
            UserSort.Name => descending
                ? query.OrderByDescending(user => user.FirstName).ThenByDescending(user => user.LastName).ThenBy(user => user.Id)
                : query.OrderBy(user => user.FirstName).ThenBy(user => user.LastName).ThenBy(user => user.Id),
            UserSort.Role => descending
                ? query.OrderByDescending(user => user.Role).ThenBy(user => user.Id)
                : query.OrderBy(user => user.Role).ThenBy(user => user.Id),
            _ => descending
                ? query.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
                : query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id)
        };
    }
}
