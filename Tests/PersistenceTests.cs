using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using CompanyHQ.Auth.Contracts.Models;
using CompanyHQ.Auth.Services;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Data;
using CompanyHQ.Postgres.Services;
using CompanyHQ.Users.Contracts.Dtos;
using CompanyHQ.Users.Contracts.Models;
using Xunit;

namespace CompanyHQ.Tests;

public sealed class PersistenceTests(FeatureFixture fixture) : IClassFixture<FeatureFixture>
{
    [Fact]
    public async Task ModelAndLegacyContractsPreserveSchemaAndAssignability()
    {
        CompanyHQ.Postgres.Contracts.Models.BaseModel original = new User { Email = "contract@example.test", PasswordHash = "test" };
        Assert.IsAssignableFrom<CompanyHQ.Postgres.Contracts.Models.BaseModel>(original);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPostgresService<User>>());
        Assert.DoesNotContain(typeof(IPersistence).Assembly.GetReferencedAssemblies(), assembly =>
            assembly.Name!.Contains("EntityFramework") || assembly.Name.Contains("Npgsql") || assembly.Name == "CompanyHQ.Postgres");
        var differ = db.GetService<IMigrationsModelDiffer>();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        Assert.False(differ.HasDifferences(initialized.GetRelationalModel(), db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetRelationalModel()));
    }

    [Fact]
    public async Task MultiModelTransactionRollsBackAndRetriesWithCleanTracking()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPersistence>();
        var prefix = "retry-" + Guid.NewGuid().ToString("N");
        var attempts = 0;
        await store.InTransactionAsync(async ct =>
        {
            attempts++;
            store.Add(new AccessRole { Name = prefix, Permissions = [] });
            store.Add(new User { Email = prefix + "@example.test", PasswordHash = "test" });
            await store.SaveChangesAsync(ct);
            if (attempts == 1) throw new PostgresException("test serialization conflict", "ERROR", "ERROR", "40001");
        }, IsolationLevel.Serializable);
        Assert.Equal(2, attempts);
        Assert.Equal(1, await store.Query<AccessRole>().CountAsync(role => role.Name == prefix));
        Assert.Equal(1, await store.Query<User>().CountAsync(user => user.Email == prefix + "@example.test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InTransactionAsync(async ct =>
        {
            store.Add(new AccessRole { Name = prefix + "-rolled-back", Permissions = [] });
            await store.SaveChangesAsync(ct);
            throw new InvalidOperationException("abort");
        }));
        Assert.False(await store.Query<AccessRole>().AnyAsync(role => role.Name == prefix + "-rolled-back"));
    }

    [Fact]
    public async Task CrudTrackingSearchPagingAndConcurrencyKeepTheirSemantics()
    {
        await using var first = fixture.Services.CreateAsyncScope();
        var repository = first.ServiceProvider.GetRequiredService<IRepository<User>>();
        var user = await repository.AddAsync(new User { Email = Guid.NewGuid().ToString("N") + "@example.test", PasswordHash = "secret", FirstName = "Case_%" });
        Assert.Equal(7, user.Id.Version);
        Assert.NotEqual(default, user.CreatedAt);
        var tracked = await first.ServiceProvider.GetRequiredService<IPersistence>().FindAsync<User>(user.Id);
        Assert.Same(user, tracked);
        var createdAt = user.CreatedAt;
        user.CreatedAt = DateTimeOffset.MinValue;
        user.LastName = "updated";
        await repository.UpdateAsync(user);
        first.ServiceProvider.GetRequiredService<IPersistence>().Clear();
        Assert.Equal(createdAt.Ticks / 10, (await repository.GetAsync(user.Id))!.CreatedAt.Ticks / 10);
        Assert.Equal("Case_%", (await repository.GetAsync(user.Id))!.FirstName);
        var page = await repository.Query().Where(row => row.Id == user.Id)
            .Search("firstName", "_%", typeof(AdminUserDto)).OrderBy(row => row.Id).ToPageAsync(0, 10);
        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(1, await repository.Query().Where(row => row.Id == user.Id && QueryFunctions.LikeIgnoreCase(row.FirstName, @"%Case\_\%%")).CountAsync());
        Assert.True(QueryFunctions.LikeIgnoreCase("Case_%", @"%Case\_\%%"));
        Assert.ThrowsAny<CompanyHQ.Persistence.Services.SearchFieldException>(() => repository.Query().Search("passwordHash", "secret", typeof(AdminUserDto)));
        var stale = (await repository.GetAsync(user.Id))!;
        await using var second = fixture.Services.CreateAsyncScope();
        Assert.True(await second.ServiceProvider.GetRequiredService<IRepository<User>>().DeleteAsync(user.Id));
        stale.LastName = "lost race";
        await Assert.ThrowsAsync<PersistenceConcurrencyException>(() => repository.UpdateAsync(stale));
        first.ServiceProvider.GetRequiredService<IPersistence>().Clear();
        Assert.False(await repository.DeleteAsync(user.Id));
    }

    [Fact]
    public async Task LockedWorkersClaimDisjointBatchesAndCommitLeases()
    {
        await using var setup = fixture.Services.CreateAsyncScope();
        var context = setup.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var user = await fixture.UserAsync(context);
        var tokens = Enumerable.Range(0, 4).Select(index => new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        }).ToArray();
        context.AddRange(tokens);
        await context.SaveChangesAsync();
        await using var first = fixture.Services.CreateAsyncScope();
        await using var second = fixture.Services.CreateAsyncScope();
        var one = first.ServiceProvider.GetRequiredService<IPersistence>();
        var two = second.ServiceProvider.GetRequiredService<IPersistence>();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var batch1 = one.InTransactionAsync(async ct =>
        {
            var batch = await one.Locked<RefreshToken>(skipLocked: true).Where(row => row.UserId == user.Id && row.RevokedAt == null)
                .OrderBy(row => row.Id).Take(2).ToListAsync(ct);
            locked.SetResult();
            await release.Task;
            foreach (var row in batch) row.RevokedAt = DateTimeOffset.UtcNow;
            await one.SaveChangesAsync(ct);
            return batch.Select(row => row.Id).ToArray();
        });
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Guid[] batch2;
        try
        {
            batch2 = await two.InTransactionAsync(async ct =>
            {
                var batch = await two.Locked<RefreshToken>(skipLocked: true).Where(row => row.UserId == user.Id && row.RevokedAt == null)
                    .OrderBy(row => row.Id).Take(2).ToListAsync(ct);
                foreach (var row in batch) row.RevokedAt = DateTimeOffset.UtcNow;
                await two.SaveChangesAsync(ct);
                return batch.Select(row => row.Id).ToArray();
            }).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { release.TrySetResult(); }
        var firstIds = await batch1;
        Assert.Equal(2, firstIds.Length);
        Assert.Equal(2, batch2.Length);
        Assert.Empty(firstIds.Intersect(batch2));
        Assert.Equal(4, await two.Query<RefreshToken>().CountAsync(row => row.UserId == user.Id && row.RevokedAt != null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => two.Locked<RefreshToken>().ToListAsync());
    }

    [Fact]
    public async Task ModerationSearchAndTargetWideDecisionsPersistAtomicHistoryAndHides()
    {
        await using var setup = fixture.Services.CreateAsyncScope();
        var db = setup.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var admin = await fixture.UserAsync(db, admin: true);
        var target = await fixture.UserAsync(db);
        var reporter = await fixture.UserAsync(db);
        var reporter2 = await fixture.UserAsync(db);
        async Task<Guid> Report(Guid id)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<CompanyHQ.Moderation.Services.IContentReportService>();
            var result = await service.CreateAsync(id, new("user", target.Id, CompanyHQ.Moderation.Contracts.Enums.ReportReason.Other, "Fixture allegation"));
            Assert.True(result.Succeeded);
            return result.Value!.Id;
        }
        var firstId = await Report(reporter.Id);
        var secondId = await Report(reporter2.Id);
        await using var decide = fixture.Services.CreateAsyncScope();
        var reports = decide.ServiceProvider.GetRequiredService<CompanyHQ.Moderation.Services.IContentReportService>();
        var list = await reports.ListAdminAsync(new(), search: CompanyHQ.Postgres.Contracts.Forms.SearchRequest.For("Fixture", "details"),
            audience: CompanyHQ.Postgres.Contracts.Enums.ResponseAudience.Admin);
        Assert.Contains(list.Items, report => report.Id == firstId);
        Assert.True(await reports.DecideAsync(admin.Id, firstId, new(CompanyHQ.Moderation.Contracts.Enums.ReportStatus.ActionTaken)));
        var store = setup.ServiceProvider.GetRequiredService<IPersistence>();
        var ids = new[] { firstId, secondId };
        Assert.Equal(2, await store.Query<CompanyHQ.Moderation.Contracts.Models.ContentReportDecision>().CountAsync(row => ids.Contains(row.ContentReportId)));
        Assert.Equal(2, await store.Query<CompanyHQ.Moderation.Contracts.Models.HiddenContent>().CountAsync(row => row.TargetId == target.Id));
        var details = Assert.IsType<CompanyHQ.Moderation.Contracts.Dtos.AdminContentReportDetailsDto>(
            await reports.GetAdminAsync(firstId, CompanyHQ.Postgres.Contracts.Enums.ResponseAudience.Admin));
        Assert.True(details.IsGloballyRestricted);
        decide.ServiceProvider.GetRequiredService<IPersistence>().Clear();
        Assert.True(await reports.DecideAsync(admin.Id, firstId, new(CompanyHQ.Moderation.Contracts.Enums.ReportStatus.Dismissed)));
        Assert.Equal(4, await store.Query<CompanyHQ.Moderation.Contracts.Models.ContentReportDecision>().CountAsync(row => ids.Contains(row.ContentReportId)));
        Assert.False(await store.Query<CompanyHQ.Moderation.Contracts.Models.HiddenContent>().AnyAsync(row => row.TargetId == target.Id));
    }

    [Fact]
    public async Task EmailWorkersCommitLeasesBeforeDeliveryAndDoNotDoubleClaim()
    {
        var prefix = "worker-" + Guid.NewGuid().ToString("N");
        await using var setup = fixture.Services.CreateAsyncScope();
        var db = setup.ServiceProvider.GetRequiredService<PostgresDbContext>();
        db.AddRange(Enumerable.Range(0, 15).Select(index => new CompanyHQ.Email.Contracts.Models.QueuedEmail
        {
            To = ["fixture@example.test"],
            Subject = "Fixture",
            Html = "secret body",
            Text = "secret body",
            IdempotencyKey = prefix + index,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        }));
        await db.SaveChangesAsync();
        await using var first = fixture.Services.CreateAsyncScope();
        await using var second = fixture.Services.CreateAsyncScope();
        var provider = new BlockingEmailProvider();
        var type = typeof(CompanyHQ.Email.Extensions.EmailServiceCollectionExtensions).Assembly
            .GetType("CompanyHQ.Email.Services.EmailQueueProcessor", throwOnError: true)!;
        var method = type.GetMethod("ProcessBatchAsync")!;
        var processor1 = ActivatorUtilities.CreateInstance(first.ServiceProvider, type, provider);
        var processor2 = ActivatorUtilities.CreateInstance(second.ServiceProvider, type, provider);
        var batch1 = (Task<int>)method.Invoke(processor1, [CancellationToken.None])!;
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Null(first.ServiceProvider.GetRequiredService<PostgresDbContext>().Database.CurrentTransaction);
            Assert.Equal(10, await setup.ServiceProvider.GetRequiredService<IPersistence>().Query<CompanyHQ.Email.Contracts.Models.QueuedEmail>()
                .CountAsync(row => row.IdempotencyKey.StartsWith(prefix) && row.NextAttemptAt > DateTimeOffset.UtcNow.AddMinutes(9)));
            Assert.Equal(5, await ((Task<int>)method.Invoke(processor2, [CancellationToken.None])!).WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally { provider.Release.TrySetResult(); }
        Assert.Equal(10, await batch1);
        Assert.Equal(15, provider.Keys.Count);
        Assert.Equal(15, provider.Keys.Distinct().Count());
        var rows = await setup.ServiceProvider.GetRequiredService<IPersistence>().Query<CompanyHQ.Email.Contracts.Models.QueuedEmail>()
            .Where(row => row.IdempotencyKey.StartsWith(prefix)).ToListAsync();
        Assert.All(rows, row =>
        {
            Assert.Equal(CompanyHQ.Email.Contracts.Enums.QueuedEmailStatus.Sent, row.Status);
            Assert.Equal("", row.Html); Assert.Equal("", row.Text);
        });
    }

    [Fact]
    public async Task AuthRotationIsAtomicAndReplayRevokesTheWinner()
    {
        await using var setup = fixture.Services.CreateAsyncScope();
        var store = setup.ServiceProvider.GetRequiredService<IPersistence>();
        if (!await store.Query<AccessRole>().AnyAsync(role => role.Name == "Client"))
        {
            store.Add(new AccessRole { Name = "Client", Permissions = [] });
            await store.SaveChangesAsync();
        }
        var auth = setup.ServiceProvider.GetRequiredService<IAuthService>();
        var registered = await auth.RegisterAsync(new(Guid.NewGuid().ToString("N") + "@example.test", "A-real-Test-password!123"), Guid.NewGuid().ToString());
        Assert.NotNull(registered.Session);
        var original = registered.Session!;
        await using var a = fixture.Services.CreateAsyncScope();
        await using var b = fixture.Services.CreateAsyncScope();
        var results = await Task.WhenAll(a.ServiceProvider.GetRequiredService<IAuthService>().RefreshAsync(original.RefreshToken),
            b.ServiceProvider.GetRequiredService<IAuthService>().RefreshAsync(original.RefreshToken));
        var winner = Assert.Single(results, result => result.Session is not null).Session!;
        await using var verify = fixture.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<IPersistence>();
        var sessions = await db.Query<RefreshToken>().Where(row => row.UserId == original.User.Id).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, row => Assert.NotNull(row.RevokedAt));
        Assert.DoesNotContain(sessions, row => row.TokenHash == original.RefreshToken || row.TokenHash == winner.RefreshToken);
        Assert.Null((await verify.ServiceProvider.GetRequiredService<IAuthService>().RefreshAsync(winner.RefreshToken)).Session);
    }
}

public sealed class RecordingAuthEmail : IAuthEmailSender
{
    public Task SendVerificationEmailAsync(string email, IssuedAuthToken token, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendPasswordResetEmailAsync(string email, IssuedAuthToken token, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendEmailChangeConfirmationAsync(string email, IssuedAuthToken token, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendPasswordChangedNoticeAsync(Guid user, string email, DateTimeOffset at, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendEmailChangingNoticeAsync(Guid user, string email, string newEmail, DateTimeOffset at, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendAccountDeletionConfirmationAsync(string email, IssuedAuthToken token, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
