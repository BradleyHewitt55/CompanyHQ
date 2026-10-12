using System.Security.Cryptography;
using System.Text;


using CompanyHQ.Services.Inbox.Contracts.Dtos;
using CompanyHQ.Services.Inbox.Contracts.Enums;
using CompanyHQ.Services.Inbox.Contracts.Models;
using CompanyHQ.Services.Inbox.Contracts.Ports;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Persistence.Services;

namespace CompanyHQ.Services.Inbox.Services;

/// <summary>
/// The conversations Inbox stores itself: the visitor half, and the same rows as the built-in
/// <c>messages</c> channel of the staff inbox.
/// </summary>
/// <remarks>
/// Takes <see cref="IPersistence"/> because a reply and the conversation's activity stamp are
/// one save.
/// </remarks>
internal sealed class VisitorConversationService(
    IPersistence context,
    TimeProvider clock,
    IInboxReplyMailer? mailer = null) : IVisitorConversationService, IInboxChannel
{
    public const string ChannelKey = "messages";

    /// <summary>How long a resolved conversation stays readable before it is deleted.</summary>
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>The list shape's fields, as the conversation row spells them.</summary>
    private static readonly IReadOnlyDictionary<string, string> SearchAliases = new Dictionary<string, string>
    {
        [nameof(AdminInboxItemDto.Title)] = nameof(InboxConversation.Name),
        [nameof(AdminInboxItemDto.Author)] = nameof(InboxConversation.Name),
        [nameof(AdminInboxItemDto.Contact)] = nameof(InboxConversation.Email),
        [nameof(AdminInboxItemDto.Preview)] = nameof(InboxConversation.Body)
    };

    public string Key => ChannelKey;

    public string Name => "Messages";

    public string Permission => AccessPermissions.ManageInbox;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<VisitorConversationAccess> StartAsync(
        VisitorConversationStart start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var conversation = new InboxConversation
        {
            Name = start.Name.Trim(),
            Email = start.Email.Trim(),
            Body = start.Message.Trim(),
            Source = start.Source.Trim(),
            Origin = string.IsNullOrWhiteSpace(start.Origin) ? null : start.Origin.Trim(),
            VisitorTokenHash = Hash(token),
            LastActivityAt = clock.GetUtcNow()
        };
        context.Add(conversation);
        await context.SaveChangesAsync(cancellationToken);

        return new VisitorConversationAccess(conversation.Id, token);
    }

    public async Task<VisitorConversationThread?> ReadAsync(
        Guid id,
        string token,
        CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        var conversation = await Readable()
            .Where(item => item.Id == id && item.VisitorTokenHash == hash)
            .Select(item => new { item.Id, item.Name, item.Body, item.CreatedAt, item.ResolvedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        var replies = await Replies(id)
            .Select(reply => new VisitorConversationReply(reply.Id, reply.Body, reply.FromVisitor, reply.CreatedAt))
            .ToListAsync(cancellationToken);

        return new VisitorConversationThread(
            conversation.Id, conversation.Name, conversation.Body, conversation.CreatedAt, conversation.ResolvedAt, replies);
    }

    public async Task<InboxError> SendAsync(
        Guid id,
        string token,
        string body,
        CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        var conversation = await context.Tracked<InboxConversation>()
            .SingleOrDefaultAsync(item => item.Id == id && item.VisitorTokenHash == hash, cancellationToken);
        if (conversation is null)
        {
            return InboxError.NotFound;
        }

        var (error, _) = await AddReplyAsync(conversation, body, fromVisitor: true, cancellationToken);
        return error;
    }

    public Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - Retention;
        return context.Tracked<InboxConversation>()
            .Where(conversation => conversation.ResolvedAt != null && conversation.ResolvedAt <= cutoff)
            .DeleteAsync(cancellationToken);
    }

    public async Task<(InboxError Error, PagedResult<AdminInboxItemDto>? Items)> ListAsync(
        Guid actingUserId,
        PageRequest paging,
        SearchRequest? search = null,
        CancellationToken cancellationToken = default)
    {
        // Open conversations first, then whichever was written in most recently.
        var page = await Readable()
            .Search(search?.Field, search?.Keyword, typeof(AdminInboxItemDto), SearchAliases)
            .OrderBy(conversation => conversation.ResolvedAt != null)
            .ThenByDescending(conversation => conversation.LastActivityAt)
            .ThenByDescending(conversation => conversation.Id)
            .ToPageAsync(
                paging.Skip,
                paging.PageSize,
                conversation => new AdminInboxItemDto(
                    ChannelKey,
                    conversation.Id.ToString(),
                    conversation.Name,
                    conversation.Name,
                    conversation.Email,
                    conversation.Body,
                    conversation.Source,
                    conversation.Origin,
                    conversation.ResolvedAt == null,
                    conversation.CreatedAt,
                    conversation.LastActivityAt,
                    null),
                cancellationToken);

        return (InboxError.None, new PagedResult<AdminInboxItemDto>(page.Items, paging.Page, paging.PageSize, page.Total));
    }

    public async Task<(InboxError Error, AdminInboxThreadDto? Thread)> GetAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default)
    {
        var conversation = Guid.TryParse(id, out var conversationId)
            ? await Readable().SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            : null;
        if (conversation is null)
        {
            return (InboxError.NotFound, null);
        }

        var open = conversation.ResolvedAt is null;
        var messages = new List<AdminInboxMessageDto>
        {
            // The first message is the row itself; the thread reads as one list.
            new(conversation.Id.ToString(), conversation.Body, conversation.Name, FromStaff: false, conversation.CreatedAt)
        };
        messages.AddRange(await Replies(conversationId)
            .Select(reply => new AdminInboxMessageDto(
                reply.Id.ToString(),
                reply.Body,
                reply.FromVisitor ? conversation.Name : "Staff",
                !reply.FromVisitor,
                reply.CreatedAt))
            .ToListAsync(cancellationToken));

        return (InboxError.None, new AdminInboxThreadDto(
            new AdminInboxItemDto(
                ChannelKey,
                conversation.Id.ToString(),
                conversation.Name,
                conversation.Name,
                conversation.Email,
                conversation.Body,
                conversation.Source,
                conversation.Origin,
                open,
                conversation.CreatedAt,
                conversation.LastActivityAt,
                Url: null),
            messages,
            CanReply: open,
            CanResolve: open,
            CanDelete: true));
    }

    public async Task<InboxError> ReplyAsync(
        Guid actingUserId,
        string id,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (await FindAsync(id, cancellationToken) is not { } conversation)
        {
            return InboxError.NotFound;
        }

        var (error, reply) = await AddReplyAsync(conversation, body, fromVisitor: false, cancellationToken);
        if (reply is not null && mailer is not null)
        {
            await mailer.SendAsync(conversation.Email, reply.Body, $"inbox-reply:{reply.Id}", cancellationToken);
        }

        return error;
    }

    public async Task<InboxError> ResolveAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default)
    {
        if (await FindAsync(id, cancellationToken) is not { } conversation)
        {
            return InboxError.NotFound;
        }

        conversation.ResolvedAt ??= clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        return InboxError.None;
    }

    public async Task<InboxError> DeleteAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default) =>
        // Replies go with the conversation through their cascading foreign key.
        Guid.TryParse(id, out var conversationId)
        && await context.Tracked<InboxConversation>()
            .Where(conversation => conversation.Id == conversationId)
            .DeleteAsync(cancellationToken) > 0
            ? InboxError.None
            : InboxError.NotFound;

    private async Task<InboxConversation?> FindAsync(string id, CancellationToken cancellationToken) =>
        Guid.TryParse(id, out var conversationId)
            ? await context.Tracked<InboxConversation>().SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            : null;

    private async Task<(InboxError Error, InboxReply? Reply)> AddReplyAsync(
        InboxConversation conversation,
        string body,
        bool fromVisitor,
        CancellationToken cancellationToken)
    {
        if (conversation.ResolvedAt is not null)
        {
            return (InboxError.Resolved, null);
        }

        var cleaned = body.Trim();
        if (cleaned.Length == 0)
        {
            return (InboxError.Invalid, null);
        }

        var reply = new InboxReply { InboxConversationId = conversation.Id, Body = cleaned, FromVisitor = fromVisitor };
        context.Add(reply);
        conversation.LastActivityAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);

        return (InboxError.None, reply);
    }

    /// <summary>Everything not yet past retention, whether or not the purge has run.</summary>
    private IQuery<InboxConversation> Readable()
    {
        var cutoff = clock.GetUtcNow() - Retention;
        return context.Query<InboxConversation>()
            .Where(conversation => conversation.ResolvedAt == null || conversation.ResolvedAt > cutoff);
    }

    private IQuery<InboxReply> Replies(Guid conversationId) =>
        context.Query<InboxReply>()
            .Where(reply => reply.InboxConversationId == conversationId)
            .OrderBy(reply => reply.CreatedAt);

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
