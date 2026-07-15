using Services.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Services.Auth.Services;

public interface IUserDirectoryService
{
    Task<IReadOnlyList<SafeUserDto>> GetUsers(CancellationToken cancellationToken = default);
}

public sealed record SafeUserDto(Guid Id, string Username, string DisplayName, string? Email, IReadOnlyList<string> Roles, IReadOnlyDictionary<string, System.Text.Json.JsonElement> Extensions)
{
    public SafeUserDto(Guid id, string username, string displayName, string? email, IReadOnlyList<string> roles) : this(id, username, displayName, email, roles, new Dictionary<string, System.Text.Json.JsonElement>()) { }
}

internal sealed class UserDirectoryService(UserManager<CompanyUser> users) : IUserDirectoryService
{
    public async Task<IReadOnlyList<SafeUserDto>> GetUsers(CancellationToken cancellationToken = default)
    {
        var result = new List<SafeUserDto>();
        foreach (var user in await users.Users.AsNoTracking().ToListAsync(cancellationToken))
            result.Add(new SafeUserDto(user.Id, user.UserName ?? string.Empty, user.DisplayName, user.Email, [.. await users.GetRolesAsync(user)], user.Extensions));
        return result;
    }
}
