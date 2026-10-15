using CompanyHQ.ServiceCatalog.Contracts;

namespace Api.Integrations;

internal static class FeatureBridgeAdmission
{
    internal static async Task<T> CallAsync<T>(string source, string target, Func<Task<T>> call)
    {
        try { return await call(); }
        catch (ServiceUnavailableException exception) when (exception.ServiceKey != source)
        { throw new ServiceDependencyUnavailableException(source, target); }
    }
}
