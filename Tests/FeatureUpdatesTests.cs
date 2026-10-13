using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Auth.Extensions;
using CompanyHQ.Services.Appointments.Contracts.Enums;
using CompanyHQ.Services.Appointments.Contracts.Forms;
using CompanyHQ.Services.Appointments.Contracts.Models;
using CompanyHQ.Services.Appointments.Extensions;
using CompanyHQ.Services.Appointments.Services;
using CompanyHQ.Services.Blogs.Contracts.Forms;
using CompanyHQ.Services.Blogs.Contracts.Ports;
using CompanyHQ.Services.Blogs.Extensions;
using CompanyHQ.Services.Blogs.Services;
using CompanyHQ.Services.Cache.Extensions;
using CompanyHQ.Services.Company.Contracts.Dtos;
using CompanyHQ.Services.Company.Contracts.Forms;
using CompanyHQ.Services.Company.Extensions;
using CompanyHQ.Services.Company.Services;
using CompanyHQ.Services.Media.Contracts.Enums;
using CompanyHQ.Services.Media.Contracts.Forms;
using CompanyHQ.Services.Media.Contracts.Models;
using CompanyHQ.Services.Media.Contracts.Ports;
using CompanyHQ.Services.Media.Extensions;
using CompanyHQ.Services.Media.Services;
using CompanyHQ.Services.Messages.Contracts;
using CompanyHQ.Services.Messages.Contracts.Dtos;
using CompanyHQ.Services.Messages.Contracts.Enums;
using CompanyHQ.Services.Messages.Contracts.Forms;
using CompanyHQ.Services.Messages.Contracts.Ports;
using CompanyHQ.Services.Messages.Extensions;
using CompanyHQ.Services.Messages.Services;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Data;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Users.Extensions;
using CompanyHQ.Services.Users.Services;
using CompanyHQ.Services.Users.Contracts.Ports;
using Testcontainers.PostgreSql;
using Xunit;

namespace CompanyHQ.Tests;

public sealed class FeatureUpdatesTests(FeatureFixture fixture) : IClassFixture<FeatureFixture>
{
    [Fact]
    public async Task AppointmentRepeats_CreateIndependentRowsAndRespectDiaryAccess()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var owner = await fixture.UserAsync(db);
        var other = await fixture.UserAsync(db);
        var appointments = scope.ServiceProvider.GetRequiredService<IAppointmentService>();
        var first = await appointments.CreateAsync(owner.Id, new("Monthly review", new(2030, 1, 31, 9, 0, 0, TimeSpan.Zero),
            Repeat: AppointmentRepeat.Monthly, Occurrences: 3));
        var rows = await db.Set<Appointment>().Where(row => row.UserId == owner.Id).OrderBy(row => row.ScheduledAt).ToListAsync();
        Assert.Equal(new[] { 31, 28, 31 }, rows.Select(row => row.ScheduledAt.Day));
        Assert.Equal(first.Id, rows[0].Id);
        Assert.Equal(3, rows.Select(row => row.Id).Distinct().Count());
        db.ChangeTracker.Clear(); // Each subsequent operation represents a separate HTTP request.
        await appointments.UpdateAsync(first.Id, owner.Id, new("Changed occurrence", first.ScheduledAt));
        Assert.Equal("Monthly review", (await appointments.GetAsync(rows[1].Id, owner.Id))!.Title);
        Assert.Null(await appointments.GetAsync(first.Id, other.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => appointments.CreateAsync(other.Id,
            new("Wrong diary", first.ScheduledAt, UserId: owner.Id, Repeat: AppointmentRepeat.Daily, Occurrences: 3)));
        Assert.True(await appointments.DeleteAsync(first.Id, owner.Id));
        Assert.Equal(2, await db.Set<Appointment>().CountAsync(row => row.UserId == owner.Id));
    }

    [Fact]
    public async Task AppointmentRepeats_RejectInvalidBoundsBeforeWriting()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var user = await fixture.UserAsync(db);
        var appointments = scope.ServiceProvider.GetRequiredService<IAppointmentService>();
        foreach (var request in new[] {
            new CreateAppointmentRequest("Invalid", DateTimeOffset.UtcNow, Occurrences: 2),
            new CreateAppointmentRequest("Invalid", DateTimeOffset.UtcNow, Repeat: AppointmentRepeat.Daily, Occurrences: 101),
            new CreateAppointmentRequest("Invalid", DateTimeOffset.UtcNow, Repeat: AppointmentRepeat.Daily, Occurrences: 0),
            new CreateAppointmentRequest("Invalid", DateTimeOffset.MaxValue, Repeat: AppointmentRepeat.Weekly, Occurrences: 2),
            new CreateAppointmentRequest("Invalid", DateTimeOffset.UtcNow, Repeat: (AppointmentRepeat)999),
        })
            await Assert.ThrowsAsync<ArgumentException>(() => appointments.CreateAsync(user.Id, request));
        Assert.Equal(0, await db.Set<Appointment>().CountAsync(row => row.UserId == user.Id));
        var weekly = new CreateAppointmentRequest("Weekly", new(2030, 1, 1, 9, 0, 0, TimeSpan.Zero), Repeat: AppointmentRepeat.Weekly, Occurrences: 100);
        await appointments.CreateAsync(user.Id, weekly);
        Assert.Equal(100, await db.Set<Appointment>().CountAsync(row => row.UserId == user.Id));
        Assert.Equal(weekly.ScheduledAt.AddDays(693), await db.Set<Appointment>().Where(row => row.UserId == user.Id).MaxAsync(row => row.ScheduledAt));
    }

    [Fact]
    public async Task CompanyEmails_NormalizePublishAndValidate_WithLegacyOmission()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var admin = await fixture.UserAsync(db, admin: true);
        var companies = scope.ServiceProvider.GetRequiredService<ICompanyProfileService>();
        var request = new UpdateCompanyProfileRequest("Test company", "https://example.test", "SUPPORT@example.test", "info@example.test", "Company description",
            PublicEmails: [new(" Support ", " SUPPORT@EXAMPLE.TEST "), new("Information", "info@example.test"), new("Sales", "Sales@example.test", " Sales enquiries ")]);
        var saved = await companies.UpdateAsync(request, admin.Id);
        Assert.Equal("support@example.test", saved.SupportEmail);
        Assert.Equal("Support", saved.PublicEmails![0].Title);
        Assert.Equal("sales@example.test", saved.PublicEmails[2].Address);
        Assert.Equal("Sales enquiries", saved.PublicEmails[2].Description);
        Assert.Equal(saved.PublicEmails, (await companies.GetAsync())!.PublicEmails);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => companies.UpdateAsync(request with
        {
            PublicEmails = [new("Support", "wrong@example.test"), new("Information", "info@example.test")]
        }, admin.Id));
        Assert.Equal(3, (await companies.GetAsync())!.PublicEmails!.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => companies.UpdateAsync(request with
        {
            PublicEmails = [new("Support", "support@example.test"), new("Information", "info@example.test"), new("Bad", "bad")]
        }, admin.Id));
        Assert.Null((await companies.UpdateAsync(request with { PublicEmails = null }, admin.Id)).PublicEmails);
    }

    [Fact]
    public async Task BlogCovers_PreserveFailedSavesAndRetireReplacedImages_WithHtmlBody()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var admin = await fixture.UserAsync(db, admin: true);
        var other = await fixture.UserAsync(db, admin: true);
        var blogs = scope.ServiceProvider.GetRequiredService<IBlogService>();
        var media = scope.ServiceProvider.GetRequiredService<IMediaService>();
        var firstCover = await UploadAsync(media, admin.Id);
        var html = "<h2>Article heading</h2><p>Paragraph with <strong>formatted content</strong>.</p>";
        var post = await blogs.CreateAsync(new("cover-" + Guid.NewGuid().ToString("N"), "Cover test", "A sufficiently long description.", html, "Test author", "Updates",
            CoverMediaObjectId: firstCover), admin.Id);
        Assert.Equal(html, post.Body);
        Assert.Equal(firstCover, post.CoverMediaObjectId);
        Assert.Contains("/api/media/", post.CoverImageUrl);
        Assert.NotNull(await media.GetPublicContentUrlAsync(firstCover));
        db.ChangeTracker.Clear();
        var update = new UpdateBlogPostRequest(post.Title, post.Description, html, post.Author, post.Category,
            CoverImageUrl: post.CoverImageUrl);
        var legacySave = await blogs.UpdateAsync(post.Slug, update, other.Id);
        Assert.Equal(firstCover, legacySave!.CoverMediaObjectId);
        db.ChangeTracker.Clear();
        var nextCover = await UploadAsync(media, admin.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => blogs.UpdateAsync(post.Slug,
            update with { CoverMediaObjectId = nextCover, IsDraft = false }, admin.Id));
        Assert.Equal(firstCover, (await blogs.GetAsync(post.Slug, ResponseAudience.Admin, admin.Id))!.CoverMediaObjectId);
        Assert.NotNull(await media.GetPublicContentUrlAsync(firstCover));
        await Assert.ThrowsAsync<ArgumentException>(() => blogs.UpdateAsync(post.Slug,
            update with { CoverMediaObjectId = nextCover }, other.Id));
        Assert.True((await media.DeleteAsync(nextCover, admin.Id)).Succeeded);
        var replacement = await UploadAsync(media, admin.Id);
        await blogs.UpdateAsync(post.Slug, update with { CoverMediaObjectId = replacement }, admin.Id);
        Assert.Null(await media.GetPublicContentUrlAsync(firstCover));
        Assert.NotNull(await media.GetPublicContentUrlAsync(replacement));
        Assert.True(await blogs.DeleteAsync(post.Slug, admin.Id));
        Assert.Null(await media.GetPublicContentUrlAsync(replacement));
    }

    [Fact]
    public async Task MessageContexts_IsolateAndReuseThreads_WithPolicyRevocation()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var sender = await fixture.UserAsync(db);
        var recipient = await fixture.UserAsync(db);
        var outsider = await fixture.UserAsync(db);
        var messages = scope.ServiceProvider.GetRequiredService<IMessageService>();
        var general = await messages.StartConversationAsync(sender.Id, new(recipient.Id));
        var tagged = await messages.StartConversationAsync(sender.Id, new(recipient.Id, "SOS", "alert-123"));
        Assert.True(general.Succeeded);
        Assert.True(tagged.Succeeded);
        Assert.NotEqual(general.Value!.Id, tagged.Value!.Id);
        Assert.Equal("sos", tagged.Value.ContextKind);
        Assert.Equal(tagged.Value.Id, (await messages.StartConversationAsync(recipient.Id, new(sender.Id, "sos", "alert-123"))).Value!.Id);
        Assert.Equal(MessageOperationStatus.Invalid, (await messages.StartConversationAsync(sender.Id, new(recipient.Id, "sos"))).Status);
        Assert.Equal(MessageOperationStatus.Invalid, (await messages.StartConversationAsync(sender.Id, new(recipient.Id, "bad:kind", "example"))).Status);
        Assert.Equal(MessageOperationStatus.Invalid, (await messages.StartConversationAsync(sender.Id, new(recipient.Id, "sos", " "))).Status);
        Assert.Equal(MessageOperationStatus.Forbidden, (await messages.StartConversationAsync(sender.Id, new(recipient.Id, "denied", "example"))).Status);
        var sent = await messages.SendMessageAsync(sender.Id, tagged.Value.Id, new(Guid.NewGuid(), "Message in the tagged thread"));
        Assert.True(sent.Succeeded);
        Assert.Equal(1, await messages.GetUnreadCountAsync(recipient.Id));
        Assert.Equal(MessageOperationStatus.NotFound, (await messages.GetMessagesAsync(outsider.Id, tagged.Value.Id)).Status);
        fixture.Policy.Deny(recipient.Id, tagged.Value.Id);
        Assert.Equal(MessageOperationStatus.NotFound, (await messages.GetMessagesAsync(recipient.Id, tagged.Value.Id)).Status);
        Assert.Equal(0, await messages.GetUnreadCountAsync(recipient.Id));
        var page = await messages.ListConversationsAsync(recipient.Id, new() { PageSize = 1 });
        Assert.Equal(1, page.Total);
        Assert.Equal(general.Value.Id, Assert.Single(page.Items).Id);
        var before = fixture.Broadcasts.Sent;
        Assert.Equal(MessageOperationStatus.Forbidden, (await messages.SendMessageAsync(sender.Id, tagged.Value.Id, new(Guid.NewGuid(), "Must not be delivered"))).Status);
        Assert.Equal(before, fixture.Broadcasts.Sent);
        var receiptsBefore = fixture.Broadcasts.Read;
        Assert.True((await messages.MarkReadAsync(sender.Id, tagged.Value.Id)).Succeeded);
        Assert.Equal(receiptsBefore, fixture.Broadcasts.Read);
        Assert.True((await messages.SendMessageAsync(sender.Id, general.Value.Id, new(Guid.NewGuid(), "General conversation still works"))).Succeeded);
        await db.Set<User>().Where(user => user.Id == sender.Id).ExecuteUpdateAsync(set => set.SetProperty(user => user.EmailVerified, false));
        Assert.Equal(MessageOperationStatus.Forbidden, (await messages.SendMessageAsync(sender.Id, general.Value.Id, new(Guid.NewGuid(), "Unverified"))).Status);
    }

    [Fact]
    public async Task MessagePolicies_RestrictAttachmentsAndStaffReads()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var sender = await fixture.UserAsync(db);
        var recipient = await fixture.UserAsync(db);
        var staff = await fixture.UserAsync(db, admin: true);
        var messages = scope.ServiceProvider.GetRequiredService<IMessageService>();
        var media = scope.ServiceProvider.GetRequiredService<IMediaService>();
        var attachment = await UploadAsync(media, sender.Id, MediaScope.Message);
        var thread = (await messages.StartConversationAsync(sender.Id, new(recipient.Id, "order", "order-123"))).Value!;
        Assert.True((await messages.SendMessageAsync(sender.Id, thread.Id, new(Guid.NewGuid(), "Attached image", attachment))).Succeeded);
        var access = scope.ServiceProvider.GetRequiredService<MessageAttachmentAccess>();
        Assert.True(await access.CanReadAsync(attachment, recipient.Id));
        Assert.False(await access.CanReadAsync(attachment, staff.Id));
        Assert.True((await messages.CloseAsync(sender.Id, thread.Id)).Succeeded);
        Assert.True(await access.CanReadAsync(attachment, staff.Id));
        fixture.Policy.Deny(staff.Id, thread.Id);
        fixture.Policy.Deny(recipient.Id, thread.Id);
        Assert.False(await access.CanReadAsync(attachment, staff.Id));
        Assert.False(await access.CanReadAsync(attachment, recipient.Id));
        Assert.Equal(MessageOperationStatus.NotFound, (await messages.GetClosedMessagesAsync(staff.Id, thread.Id)).Status);
        Assert.Equal(0, (await messages.ListClosedConversationsAsync(staff.Id, new())).Total);
        Assert.Equal(MessageOperationStatus.NotFound, (await messages.DeleteAsync(staff.Id, thread.Id)).Status);
        Assert.True((await messages.GetMessagesAsync(sender.Id, thread.Id)).Succeeded);
    }

    [Fact]
    public async Task AccountDeletion_RequiresConversationRemovalAndKeepsOtherAccount()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var owner = await fixture.UserAsync(db);
        var other = await fixture.UserAsync(db);
        var messages = scope.ServiceProvider.GetRequiredService<IMessageService>();
        var deletion = scope.ServiceProvider.GetRequiredService<IAccountDeletionCoordinator>();
        var thread = (await messages.StartConversationAsync(owner.Id, new(other.Id))).Value!;

        var refused = await deletion.DeleteAsync(owner.Id);
        Assert.Equal(AccountDeletionOutcome.Blocked, refused.Result);
        Assert.Contains("conversations", refused.Reason);
        Assert.True(await db.Set<User>().AnyAsync(row => row.Id == owner.Id));
        Assert.True(await db.Set<CompanyHQ.Services.Messages.Contracts.Models.Conversation>().AnyAsync(row => row.Id == thread.Id));

        Assert.True((await messages.DeleteAsync(owner.Id, thread.Id)).Succeeded);
        Assert.Equal(AccountDeletionOutcome.Deleted, (await deletion.DeleteAsync(owner.Id)).Result);
        Assert.False(await db.Set<User>().AnyAsync(row => row.Id == owner.Id));
        Assert.True(await db.Set<User>().AnyAsync(row => row.Id == other.Id));
    }

    private static async Task<Guid> UploadAsync(IMediaService media, Guid owner, MediaScope scope = MediaScope.Content)
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aNuoAAAAASUVORK5CYII=");
        var ticket = await media.CreateUploadAsync(new(scope, "image/png", bytes.Length), owner);
        await using var stream = new MemoryStream(bytes);
        Assert.True((await media.UploadAsync(ticket.MediaObjectId, owner, stream, "image/png", bytes.Length)).Succeeded);
        Assert.True((await media.CompleteAsync(ticket.MediaObjectId, owner)).Succeeded);
        return ticket.MediaObjectId;
    }
}

public sealed class FeatureFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgis/postgis:17-3.5").Build();
    private readonly IContainer redis = new ContainerBuilder().WithImage("redis:7.4-alpine").WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379)).Build();
    private readonly FileObjectStorage storage = new();
    public ServiceProvider Services { get; private set; } = null!;
    public ContextPolicy Policy { get; } = new();
    public RecordingBroadcaster Broadcasts { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(), redis.StartAsync());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:ConnectionString"] = postgres.GetConnectionString(),
            ["Postgres:AutoMigrate"] = "false",
            ["Postgres:AutoSeed"] = "false",
            ["Cache:ConnectionString"] = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}",
            ["Media:ContentBaseUrl"] = "http://localhost:4002",
            ["Media:Bucket"] = "test-media",
            ["Jwt:SigningKey"] = "test-only-signing-key-at-least-thirty-two-characters",
            ["Jwt:Issuer"] = "tests",
            ["Jwt:Audience"] = "tests",
            ["Auth:WebBaseAddress"] = "https://example.test",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddPostgres(configuration, typeof(Program).Assembly);
        services.AddUsers(configuration);
        services.AddAuth(configuration);
        services.AddSingleton<CompanyHQ.Services.Auth.Services.IAuthEmailSender, RecordingAuthEmail>();
        services.AddRedisConnection(configuration);
        services.AddCache(configuration);
        services.AddAppointments();
        services.AddCompany();
        services.AddBlogs();
        services.AddMedia(configuration);
        services.AddMessages(configuration);
        CompanyHQ.Services.Moderation.Extensions.ModerationServiceCollectionExtensions.AddModeration(services);
        services.AddSingleton<IMessageConversationPolicy>(Policy);
        services.AddSingleton<IMessageBroadcaster>(Broadcasts);
        services.AddSingleton<IMediaObjectStorageProvider>(storage);
        // Exercise the actual host bridges without widening their production visibility for tests.
        foreach (var (port, name) in new[] {
            (typeof(IBlogCoverImages), "BlogCoverImages"),
            (typeof(IMessageAttachments), "MessageAttachments"),
        }) services.AddScoped(port, typeof(Program).Assembly.GetType($"Api.Integrations.{name}", throwOnError: true)!);
        Services = services.BuildServiceProvider();
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PostgresDbContext>().Database.MigrateAsync();
    }

    public async Task<User> UserAsync(PostgresDbContext db, bool admin = false)
    {
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@example.test",
            PasswordHash = "test-only",
            EmailVerified = true,
            Role = admin ? UserRole.Admin : UserRole.Client
        };
        db.Add(user);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return user;
    }

    public async Task DisposeAsync()
    {
        if (Services is not null) await Services.DisposeAsync();
        await postgres.DisposeAsync();
        await redis.DisposeAsync();
        storage.Dispose();
    }
}

public sealed class ContextPolicy : IMessageConversationPolicy
{
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> denied = new();
    public void Deny(Guid actor, Guid thread) => denied.GetOrAdd(actor, _ => []).Add(thread);
    public Task<bool> CanStartAsync(Guid actor, Guid other, string? kind, string? id, CancellationToken ct = default) =>
        Task.FromResult(kind != "denied");
    public Task<IReadOnlySet<Guid>> RestrictedConversationIdsAsync(Guid actor, ConversationAction action, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(denied.TryGetValue(actor, out var ids) ? ids : new HashSet<Guid>());
}

public sealed class RecordingBroadcaster : IMessageBroadcaster
{
    public int Sent { get; private set; }
    public int Read { get; private set; }
    public Task BroadcastMessageAsync(ClientMessageDto message, Guid sender, Guid recipient, CancellationToken ct = default)
    { Sent++; return Task.CompletedTask; }
    public Task BroadcastReadAsync(Guid conversation, DateTimeOffset at, Guid reader, Guid other, CancellationToken ct = default)
    { Read++; return Task.CompletedTask; }
}

internal sealed class FileObjectStorage : IMediaObjectStorageProvider, IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "companyhq-test-storage-" + Guid.NewGuid().ToString("N"));
    private readonly ConcurrentDictionary<string, string> types = new();
    public string CreateUploadUrl(string key, string type, DateTimeOffset expires) => $"https://storage.example.test/{key}";
    public string CreateDownloadUrl(string key, DateTimeOffset expires) => $"https://storage.example.test/{key}";
    public async Task<bool> PutAsync(string key, string type, Stream content, long length, CancellationToken ct = default)
    {
        var path = Path.Combine(root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        types[key] = type;
        return true;
    }
    public Task<StoredObjectMetadata?> GetMetadataAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(types.TryGetValue(key, out var type) ? new StoredObjectMetadata(type, new FileInfo(Path.Combine(root, key)).Length) : null);
    public Task<StorageDeleteOutcome> DeleteAsync(string key, CancellationToken ct = default)
    { File.Delete(Path.Combine(root, key)); return Task.FromResult(StorageDeleteOutcome.AlreadyGone); }
    public void Dispose()
    {
        var resolved = Path.GetFullPath(root);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("companyhq-test-storage-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected test storage directory.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }
}
