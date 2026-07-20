namespace Abstractions.Companies;

public interface IServiceScaffolder
{
    Task ScaffoldAsync(string serviceName, CancellationToken cancellationToken = default);
}
