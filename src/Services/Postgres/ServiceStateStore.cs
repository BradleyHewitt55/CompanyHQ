using System.Collections.Concurrent;
using Services.Companies.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Services.Postgres;

public sealed class ServiceStateStore(IServiceScopeFactory scopeFactory) : IServiceStateStore
{
    private readonly ConcurrentDictionary<string, (bool Enabled, long Sequence)> states = new(StringComparer.OrdinalIgnoreCase);
    private long sequence;
    public event EventHandler<ServiceStateChangedEventArgs>? Changed;

    public bool IsEnabled(string serviceId) => !states.TryGetValue(serviceId, out var state) || state.Enabled;
    public bool Contains(string serviceId) => states.ContainsKey(serviceId);
    public long EnrollmentSequence(string serviceId) => states.TryGetValue(serviceId, out var state) ? state.Sequence : long.MaxValue;

    public async Task Enroll(string serviceId, bool enabled = true, CancellationToken cancellationToken = default)
    {
        if (Contains(serviceId)) return;
        await using var scope = scopeFactory.CreateAsyncScope();
        var entities = scope.ServiceProvider.GetRequiredService<IEntityService<ServiceState>>();
        var next = Math.Max(sequence, (await entities.Get(cancellationToken)).Select(state => state.EnrollmentSequence).DefaultIfEmpty().Max()) + 1;
        var state = new ServiceState { ServiceId = serviceId, Enabled = enabled, EnrollmentSequence = next };
        await entities.Save(state, cancellationToken);
        sequence = next;
        states[serviceId] = (enabled, next);
        Changed?.Invoke(this, new(serviceId, enabled, next, true));
    }

    public async Task SetEnabled(string serviceId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!Contains(serviceId)) await Enroll(serviceId, enabled, cancellationToken);
        var current = states[serviceId];
        states[serviceId] = (enabled, current.Sequence);
        await using var scope = scopeFactory.CreateAsyncScope();
        var entities = scope.ServiceProvider.GetRequiredService<IEntityService<ServiceState>>();
        var existing = (await entities.GetBy(state => state.ServiceId == serviceId, cancellationToken)).SingleOrDefault();
        await entities.Save(existing is null
            ? new ServiceState { ServiceId = serviceId, Enabled = enabled, EnrollmentSequence = current.Sequence }
            : Update(existing, enabled), cancellationToken);
        Changed?.Invoke(this, new(serviceId, enabled, current.Sequence, false));
    }

    public async Task Load(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var entities = scope.ServiceProvider.GetRequiredService<IEntityService<ServiceState>>();
        foreach (var state in await entities.Get(cancellationToken))
        {
            states[state.ServiceId] = (state.Enabled, state.EnrollmentSequence);
            sequence = Math.Max(sequence, state.EnrollmentSequence);
        }
    }

    private static ServiceState Update(ServiceState state, bool enabled) { state.Enabled = enabled; return state; }
}
