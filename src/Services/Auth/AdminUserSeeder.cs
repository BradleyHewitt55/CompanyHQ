using Services.Auth.Models;
using Microsoft.AspNetCore.Identity;

namespace Services.Auth;

public sealed class AdminUserSeeder(UserManager<CompanyUser> users, RoleManager<IdentityRole<Guid>> roles, Microsoft.Extensions.Configuration.IConfiguration configuration)
{
    public async Task<SeededOwner> SeedOwner(string username, CancellationToken cancellationToken = default)
    {
        foreach (var role in AuthRoles.All)
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));

        var existing = await users.FindByNameAsync(username);
        if (existing is not null) return new SeededOwner(existing.Id, username, null, false);
        var password = PasswordGenerator.Generate();
        var company = configuration["Company:Id"] ?? "local";
        var user = new CompanyUser { UserName = username, DisplayName = username, Email = username.Contains('@') ? username : $"{username}@{company}.companyhq.local" };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        await users.AddToRoleAsync(user, AuthRoles.Owner);
        return new SeededOwner(user.Id, username, password, true);
    }
}

public sealed record SeededOwner(Guid UserId, string Username, string? Password, bool Created);
