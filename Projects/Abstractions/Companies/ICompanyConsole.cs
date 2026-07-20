namespace Abstractions.Companies;

public interface ICompanyConsole
{
    Task<string?> ReadLineAsync(CancellationToken cancellationToken = default);
    Task<string?> ReadSecretAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(string value, CancellationToken cancellationToken = default);
    Task WriteLineAsync(string value, CancellationToken cancellationToken = default);
}
