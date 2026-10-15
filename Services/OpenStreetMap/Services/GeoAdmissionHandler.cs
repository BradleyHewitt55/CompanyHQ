using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.OpenStreetMap.Services;

/// <summary>Rechecks committed admission after pacing, immediately before outbound HTTP.</summary>
internal sealed class GeoAdmissionHandler(IServiceAvailability? availability = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (availability is not null) await availability.RequireAsync("geo", true, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
