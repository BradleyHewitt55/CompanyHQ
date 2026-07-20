using Abstractions.Data;

namespace Abstractions.Users;

public interface IUserService
{
    Task<User> SeedAdminAsync(NewUser details, CancellationToken cancellationToken = default);
    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<User>> QueryAsync(UserQuery query, CancellationToken cancellationToken = default);
    Task<User> SaveAsync(UserSave details, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}
