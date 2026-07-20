using Abstractions.Companies;

namespace IntegrationTests;

internal sealed class ScriptedConsole(IEnumerable<string?> lines, IEnumerable<string?> secrets)
    : ICompanyConsole
{
    private readonly Queue<string?> lines = new(lines);
    private readonly Queue<string?> secrets = new(secrets);
    public List<string> Output { get; } = [];
    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default) => Task.FromResult(lines.Dequeue());
    public Task<string?> ReadSecretAsync(CancellationToken cancellationToken = default) => Task.FromResult(secrets.Dequeue());
    public Task WriteAsync(string value, CancellationToken cancellationToken = default) { Output.Add(value); return Task.CompletedTask; }
    public Task WriteLineAsync(string value, CancellationToken cancellationToken = default) { Output.Add(value); return Task.CompletedTask; }
}
