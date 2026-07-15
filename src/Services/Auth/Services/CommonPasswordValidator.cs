using Services.Auth.Models;
using Microsoft.AspNetCore.Identity;

namespace Services.Auth.Services;

internal sealed class CommonPasswordValidator : IPasswordValidator<CompanyUser>
{
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase) { "password", "password123", "qwerty123456", "letmein123456" };
    public Task<IdentityResult> ValidateAsync(UserManager<CompanyUser> manager, CompanyUser user, string? password) =>
        Task.FromResult(password is not null && !Common.Contains(password)
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError { Code = "CommonPassword", Description = "The password is too common." }));
}
