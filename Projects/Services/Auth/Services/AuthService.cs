using Abstractions.Data;
using Abstractions.Users;
using CompanyHQ.Services.Auth.Models;
using Microsoft.AspNetCore.Identity;

namespace CompanyHQ.Services.Auth.Services;

public interface IAuthService
{
    Task<User?> LoginAsync(LoginModel login, CancellationToken cancellationToken = default);
    Task<User?> RegisterAsync(RegisterModel registration, CancellationToken cancellationToken = default);
    Task<User?> RegisterClientAsync(RegisterModel registration, CancellationToken cancellationToken = default);
}

public sealed class AuthService(
    IRepository<User> repository,
    IQueryService<User> queries,
    IPasswordHasher<User> passwordHasher) : IAuthService
{
    // A well-formed hash of a throwaway password, verified when the account does
    // not exist so a miss costs the same time as a wrong password — otherwise the
    // response time reveals which emails have accounts.
    private static readonly User Nobody = new()
    {
        Name = string.Empty,
        Surname = string.Empty,
        Email = string.Empty,
        ContactNumber = string.Empty,
        Role = string.Empty,
        PasswordHash = string.Empty,
    };
    private static readonly string NobodyHash =
        new PasswordHasher<User>().HashPassword(Nobody, Guid.NewGuid().ToString("N"));

    public async Task<User?> LoginAsync(
        LoginModel login,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(login.Email);
        var user = await queries.FirstOrDefaultAsync(
            candidate => candidate.Email == email, cancellationToken);
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(Nobody, NobodyHash, login.Password);
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, login.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        // The hasher's cost parameters can change between releases; a successful
        // login is the only moment the plaintext is available to re-hash with them.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, login.Password);
            await repository.UpdateAsync(user, cancellationToken);
        }

        return user;
    }

    public async Task<User?> RegisterAsync(
        RegisterModel registration,
        CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsValid(registration.Password))
        {
            throw new ArgumentException(PasswordPolicy.Requirement, nameof(registration));
        }

        if (await queries.CountAsync(cancellationToken: cancellationToken) > 0)
        {
            return null;
        }

        var user = new User
        {
            Name = registration.Name.Trim(),
            Surname = registration.Surname.Trim(),
            Email = NormalizeEmail(registration.Email),
            ContactNumber = registration.ContactNumber.Trim(),
            Role = Roles.Admin,
            PasswordHash = string.Empty,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, registration.Password);
        return await repository.AddAsync(user, cancellationToken);
    }

    public async Task<User?> RegisterClientAsync(
        RegisterModel registration,
        CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsValid(registration.Password))
        {
            throw new ArgumentException(PasswordPolicy.Requirement, nameof(registration));
        }

        var user = new User
        {
            Name = registration.Name.Trim(),
            Surname = registration.Surname.Trim(),
            Email = NormalizeEmail(registration.Email),
            ContactNumber = registration.ContactNumber.Trim(),
            Role = Roles.Client,
            PasswordHash = string.Empty,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, registration.Password);
        return await repository.AddAsync(user, cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
