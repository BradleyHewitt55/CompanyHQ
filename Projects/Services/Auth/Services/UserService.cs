using System.Linq.Expressions;
using Abstractions.Data;
using Abstractions.Users;
using Microsoft.AspNetCore.Identity;

namespace CompanyHQ.Services.Auth.Services;

public sealed class UserService(
    IRepository<User> repository,
    IQueryService<User> queries,
    IPasswordHasher<User> passwordHasher) : IUserService
{
    public async Task<User> SeedAdminAsync(
        NewUser details,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(details.Email);
        var existing = await queries.FirstOrDefaultAsync(
            user => user.Email == email, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (!PasswordPolicy.IsValid(details.Password))
        {
            throw new ArgumentException(PasswordPolicy.Requirement, nameof(details));
        }

        var user = new User
        {
            Name = details.Name.Trim(),
            Surname = details.Surname.Trim(),
            Email = email,
            ContactNumber = details.ContactNumber.Trim(),
            Role = Roles.Admin,
            PasswordHash = string.Empty,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, details.Password);
        return await repository.AddAsync(user, cancellationToken);
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => await queries.CountAsync(cancellationToken: cancellationToken) > 0;

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => queries.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => queries.FirstOrDefaultAsync(
            user => user.Email == NormalizeEmail(email), cancellationToken);

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
        => queries.ListAsync(cancellationToken: cancellationToken);

    public Task<PagedResult<User>> QueryAsync(
        UserQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var search = query.Search?.Trim().ToLowerInvariant();
        var role = query.Role?.Trim();
        Expression<Func<User, bool>>? predicate = (search, role) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => user =>
                (user.Name.ToLower().Contains(search)
                    || user.Surname.ToLower().Contains(search)
                    || user.Email.ToLower().Contains(search))
                && user.Role == role,
            ({ Length: > 0 }, _) => user =>
                user.Name.ToLower().Contains(search)
                || user.Surname.ToLower().Contains(search)
                || user.Email.ToLower().Contains(search),
            (_, { Length: > 0 }) => user => user.Role == role,
            _ => null,
        };

        return queries.PageAsync(
            page,
            pageSize,
            predicate,
            user => user.Email,
            cancellationToken: cancellationToken);
    }

    public async Task<User> SaveAsync(
        UserSave details,
        CancellationToken cancellationToken = default)
    {
        if (details.Role is not (Roles.Admin or Roles.Client))
        {
            throw new ArgumentException("Role must be Admin or Client.", nameof(details));
        }

        if (details.Password is not null && !PasswordPolicy.IsValid(details.Password))
        {
            throw new ArgumentException(PasswordPolicy.Requirement, nameof(details));
        }

        if (details.Id is null)
        {
            if (details.Password is null)
            {
                throw new ArgumentException("Password is required when creating a user.", nameof(details));
            }

            var created = new User
            {
                Name = details.Name.Trim(),
                Surname = details.Surname.Trim(),
                Email = NormalizeEmail(details.Email),
                ContactNumber = details.ContactNumber.Trim(),
                Role = details.Role,
                PasswordHash = string.Empty,
            };
            created.PasswordHash = passwordHasher.HashPassword(created, details.Password);
            return await repository.AddAsync(created, cancellationToken);
        }

        var existing = await repository.GetAsync(details.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException("User was not found.");
        existing.Name = details.Name.Trim();
        existing.Surname = details.Surname.Trim();
        existing.Email = NormalizeEmail(details.Email);
        existing.ContactNumber = details.ContactNumber.Trim();
        existing.Role = details.Role;
        if (details.Password is not null)
        {
            existing.PasswordHash = passwordHasher.HashPassword(existing, details.Password);
        }

        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = await repository.GetAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("User was not found.");
        await repository.DeleteAsync(id, cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
