using System.Linq.Expressions;

using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Users.Contracts.Dtos;

/// <summary>SQL projections for each account audience.</summary>
public static class UserProjections
{
    /// <summary>The operator shape's role name, which the account reaches through its role row.</summary>
    public static IReadOnlyDictionary<string, string> SearchAliases { get; } = new Dictionary<string, string>
    {
        [nameof(AdminUserDto.RoleName)] = "AccessRole.Name"
    };

    public static Expression<Func<User, ClientUserDto>> ToClient { get; } =
        user => new ClientUserDto(user.FirstName, user.LastName, user.Bio, user.ProfileImageUrl, user.CreatedAt);

    public static Expression<Func<User, OwnerUserDto>> ToOwner { get; } =
        user => new OwnerUserDto(user.Id, user.FirstName, user.LastName, user.Bio, user.ProfileImageUrl, user.CreatedAt);

    public static Expression<Func<User, ClientUserLookupDto>> ToLookup { get; } =
        user => new ClientUserLookupDto(user.Id, user.FirstName, user.LastName);

    public static Expression<Func<User, AdminUserDto>> ToAdmin { get; } =
        user => new AdminUserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Bio,
            user.ProfileImageUrl,
            user.Role,
            user.CreatedAt,
            user.UpdatedAt,
            user.McpDisabledTools,
            user.AccessRoleId,
            user.AccessRole == null ? null : user.AccessRole.Name,
            user.AccessRole == null ? null : user.AccessRole.Permissions);

    private static Expression<Func<User, ClientUserDto>> ToOwnerAsClient { get; } =
        user => (ClientUserDto)new OwnerUserDto(user.Id, user.FirstName, user.LastName, user.Bio, user.ProfileImageUrl, user.CreatedAt);

    private static Expression<Func<User, ClientUserDto>> ToAdminAsClient { get; } =
        user => (ClientUserDto)new AdminUserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Bio,
            user.ProfileImageUrl,
            user.Role,
            user.CreatedAt,
            user.UpdatedAt,
            user.McpDisabledTools,
            user.AccessRoleId,
            user.AccessRole == null ? null : user.AccessRole.Name,
            user.AccessRole == null ? null : user.AccessRole.Permissions);

    public static Expression<Func<User, ClientUserDto>> For(ResponseAudience audience) =>
        audience switch
        {
            ResponseAudience.Admin => ToAdminAsClient,
            ResponseAudience.Owner => ToOwnerAsClient,
            _ => ToClient
        };

    public static AdminUserDto AsAdmin(this User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.Bio, user.ProfileImageUrl, user.Role, user.CreatedAt, user.UpdatedAt, user.McpDisabledTools, user.AccessRoleId, user.AccessRole?.Name, user.AccessRole?.Permissions);
}
