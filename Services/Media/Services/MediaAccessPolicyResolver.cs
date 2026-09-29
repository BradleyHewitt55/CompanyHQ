using CompanyHQ.Services.Media.Contracts.Ports;
using CompanyHQ.Services.Media.Contracts.Enums;

namespace CompanyHQ.Services.Media.Services;

/// <summary>
/// Hands every private read to the owning feature's policy and fails closed when none is registered.
/// There is no administrator shortcut: who may read a private object, operators included, is the
/// owning feature's rule, so Media can never show an operator more than that feature would.
/// </summary>
public sealed class MediaAccessPolicyResolver : IMediaAccessPolicyResolver
{
    private readonly IReadOnlyDictionary<MediaScope, IMediaAccessPolicy> policies;

    public MediaAccessPolicyResolver(IEnumerable<IMediaAccessPolicy> registeredPolicies)
    {
        ArgumentNullException.ThrowIfNull(registeredPolicies);

        var resolved = new Dictionary<MediaScope, IMediaAccessPolicy>();
        foreach (var policy in registeredPolicies)
        {
            ArgumentNullException.ThrowIfNull(policy);

            if (!Enum.IsDefined(policy.Scope))
            {
                throw new InvalidOperationException(
                    $"Media access policy {policy.GetType().Name} declares an undefined scope.");
            }

            if (!resolved.TryAdd(policy.Scope, policy))
            {
                throw new InvalidOperationException(
                    $"More than one media access policy is registered for {policy.Scope}.");
            }
        }

        policies = resolved;
    }

    public Task<bool> CanReadAsync(
        MediaScope scope,
        Guid mediaObjectId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!policies.TryGetValue(scope, out var policy))
        {
            // A missing registration is a deployment defect, not permission to read the object.
            throw new InvalidOperationException(
                $"No IMediaAccessPolicy is registered for media scope {scope}; refusing the read.");
        }

        return policy.CanReadAsync(mediaObjectId, viewerUserId, cancellationToken);
    }
}
