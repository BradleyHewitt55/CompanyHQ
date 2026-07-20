using System.Linq.Expressions;
using Abstractions.Data;
using Abstractions.Users;
using CompanyHQ.Services.Auth.Models;
using CompanyHQ.Services.Auth.Services;
using Microsoft.AspNetCore.Identity;

namespace UnitTests;

public sealed class UserServiceTests
{
    [Fact]
    public async Task SeedAdminIsIdempotentAndAuthServiceValidatesPassword()
    {
        var store = new UserStore();
        var hasher = new PasswordHasher<User>();
        var users = new UserService(store, store, hasher);
        var auth = new AuthService(store, store, hasher);
        var details = new NewUser("Ada", "Lovelace", "ADA@example.com", "123", "plain-text");

        var first = await users.SeedAdminAsync(details);
        var second = await users.SeedAdminAsync(details);

        Assert.Same(first, second);
        Assert.Single(store.Users);
        Assert.Equal(Roles.Admin, first.Role);
        Assert.NotEqual(details.Password, first.PasswordHash);
        Assert.Same(first, await auth.LoginAsync(new LoginModel
        {
            Email = "ada@example.com",
            Password = details.Password,
        }));
        Assert.Null(await auth.LoginAsync(new LoginModel
        {
            Email = "ada@example.com",
            Password = "wrong",
        }));
    }

    [Fact]
    public async Task SaveCreatesAndUpdatesWithoutReplacingThePasswordHash()
    {
        var store = new UserStore();
        var users = new UserService(store, store, new PasswordHasher<User>());

        var created = await users.SaveAsync(new UserSave(
            null, "Grace", "Hopper", " GRACE@example.com ", "123", Roles.Client, "secret-pass"));
        var originalHash = created.PasswordHash;

        var updated = await users.SaveAsync(new UserSave(
            created.Id, "Grace", "Murray Hopper", "grace@example.com", "456", Roles.Admin, null));

        Assert.Equal("grace@example.com", created.Email);
        Assert.NotEqual("secret-pass", originalHash);
        Assert.Equal(originalHash, updated.PasswordHash);
        Assert.Equal("Murray Hopper", updated.Surname);
        Assert.Equal(Roles.Admin, updated.Role);
    }

    [Fact]
    public async Task SaveRejectsUnknownRoleAndCreateWithoutPassword()
    {
        var store = new UserStore();
        var users = new UserService(store, store, new PasswordHasher<User>());

        await Assert.ThrowsAsync<ArgumentException>(() => users.SaveAsync(new UserSave(
            null, "Grace", "Hopper", "grace@example.com", "123", "Owner", "secret-pass")));
        await Assert.ThrowsAsync<ArgumentException>(() => users.SaveAsync(new UserSave(
            null, "Grace", "Hopper", "grace@example.com", "123", Roles.Client, null)));
    }

    [Fact]
    public async Task QueryFiltersBySearchAndRoleThenPages()
    {
        var store = new UserStore();
        var users = new UserService(store, store, new PasswordHasher<User>());
        await users.SaveAsync(new UserSave(null, "Ada", "Lovelace", "ada@example.com", "1", Roles.Client, "secret-pass"));
        await users.SaveAsync(new UserSave(null, "Adam", "Admin", "adam@example.com", "2", Roles.Admin, "secret-pass"));
        await users.SaveAsync(new UserSave(null, "Grace", "Hopper", "grace@example.com", "3", Roles.Client, "secret-pass"));

        var result = await users.QueryAsync(new UserQuery(1, 1, "AD", Roles.Client));

        var user = Assert.Single(result.Items);
        Assert.Equal("ada@example.com", user.Email);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task RegisterClientAssignsClientRoleWhenUsersAlreadyExist()
    {
        var store = new UserStore();
        var hasher = new PasswordHasher<User>();
        var users = new UserService(store, store, hasher);
        var auth = new AuthService(store, store, hasher);
        await users.SeedAdminAsync(new NewUser(
            "Ada", "Admin", "ada@example.com", "1", "secret-pass"));

        var client = await auth.RegisterClientAsync(new RegisterModel
        {
            Name = "Grace",
            Surname = "Client",
            Email = "GRACE@example.com",
            ContactNumber = "2",
            Password = "client-pass",
            Confirmation = "client-pass",
        });

        Assert.NotNull(client);
        Assert.Equal(Roles.Client, client.Role);
        Assert.Equal("grace@example.com", client.Email);
        Assert.Equal(2, store.Users.Count);
    }

    private sealed class UserStore : IRepository<User>, IQueryService<User>
    {
        public List<User> Users { get; } = [];
        public Task<User> AddAsync(User entity, CancellationToken cancellationToken = default)
        {
            Users.Add(entity);
            return Task.FromResult(entity);
        }
        public Task<User?> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.AsQueryable().FirstOrDefault(predicate));
        public Task<int> CountAsync(Expression<Func<User, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(predicate is null ? Users.Count : Users.AsQueryable().Count(predicate));
        public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(user => user.Id == id));
        public Task<IReadOnlyList<User>> ListAsync(Expression<Func<User, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            IEnumerable<User> result = predicate is null ? Users : Users.AsQueryable().Where(predicate);
            return Task.FromResult<IReadOnlyList<User>>(result.ToList());
        }
        public Task<PagedResult<User>> PageAsync(int page, int pageSize, Expression<Func<User, bool>>? predicate = null, Expression<Func<User, object>>? orderBy = null, bool descending = false, CancellationToken cancellationToken = default)
        {
            IQueryable<User> result = Users.AsQueryable();
            if (predicate is not null) result = result.Where(predicate);
            var totalCount = result.Count();
            result = orderBy is null
                ? result.OrderBy(user => user.Id)
                : descending ? result.OrderByDescending(orderBy) : result.OrderBy(orderBy);
            return Task.FromResult(new PagedResult<User>(
                result.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                page,
                pageSize,
                totalCount));
        }
        public Task UpdateAsync(User entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Users.RemoveAll(user => user.Id == id);
            return Task.CompletedTask;
        }
    }
}
