namespace Services.Companies.Contracts.Services;

public interface IServiceStateStore
{
    event EventHandler<ServiceStateChangedEventArgs>? Changed;
    bool IsEnabled(string serviceId);
    bool Contains(string serviceId);
    long EnrollmentSequence(string serviceId);
    Task Enroll(string serviceId, bool enabled = true, CancellationToken cancellationToken = default);
    Task SetEnabled(string serviceId, bool enabled, CancellationToken cancellationToken = default);
}

public sealed record ServiceStateChangedEventArgs(string ServiceId, bool Enabled, long EnrollmentSequence, bool Enrolled);

public sealed class InMemoryServiceStateStore : IServiceStateStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (bool Enabled, long Sequence)> states = new(StringComparer.OrdinalIgnoreCase);
    private long sequence;
    public event EventHandler<ServiceStateChangedEventArgs>? Changed;
    public bool IsEnabled(string serviceId) => !states.TryGetValue(serviceId, out var state) || state.Enabled;
    public bool Contains(string serviceId) => states.ContainsKey(serviceId);
    public long EnrollmentSequence(string serviceId) => states.TryGetValue(serviceId, out var state) ? state.Sequence : long.MaxValue;
    public Task Enroll(string serviceId, bool enabled = true, CancellationToken cancellationToken = default)
    {
        var enrolled = false;
        var state = states.GetOrAdd(serviceId, _ => { enrolled = true; return (enabled, Interlocked.Increment(ref sequence)); });
        if (enrolled) Changed?.Invoke(this, new(serviceId, state.Enabled, state.Sequence, true));
        return Task.CompletedTask;
    }
    public async Task SetEnabled(string serviceId, bool enabled, CancellationToken cancellationToken = default)
    {
        await Enroll(serviceId, enabled, cancellationToken);
        states.AddOrUpdate(serviceId, _ => (enabled, Interlocked.Increment(ref sequence)), (_, state) => (enabled, state.Sequence));
        var current = states[serviceId];
        Changed?.Invoke(this, new(serviceId, current.Enabled, current.Sequence, false));
    }
}

public interface IServiceStartupTask
{
    Task Run(IServiceProvider services, CancellationToken cancellationToken = default);
}
