namespace Services.Companies.Contracts.Services;

public interface IRuntimeServiceLoader
{
    Task<RuntimeServiceLoadResult> BuildAndLoad(string serviceDirectory, CancellationToken cancellationToken = default);
}

public sealed record RuntimeServiceLoadResult(bool Loaded, string? ServiceId = null, string? Error = null);
