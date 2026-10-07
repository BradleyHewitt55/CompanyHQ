using CompanyHQ.Services.Messages.Contracts.Enums;
using CompanyHQ.Services.Messages.Contracts.Ports;

namespace CompanyHQ.Services.Messages.Services;

/// <summary>Combines host policies by intersection: any denial wins, and no policy grants access.</summary>
public sealed class MessageConversationRules(IEnumerable<IMessageConversationPolicy> policies)
{
    public async Task<bool> CanStartAsync(Guid actor, Guid participant, string? kind, string? id, CancellationToken ct)
    {
        foreach (var policy in policies)
            if (!await policy.CanStartAsync(actor, participant, kind, id, ct)) return false;
        return true;
    }

    public async Task<IReadOnlySet<Guid>> RestrictedAsync(Guid actor, ConversationAction action, CancellationToken ct)
    {
        var restricted = new HashSet<Guid>();
        foreach (var policy in policies)
            restricted.UnionWith(await policy.RestrictedConversationIdsAsync(actor, action, ct));
        return restricted;
    }

    public async Task<bool> AllowsAsync(Guid actor, Guid conversationId, ConversationAction action, CancellationToken ct) =>
        !(await RestrictedAsync(actor, action, ct)).Contains(conversationId);
}
