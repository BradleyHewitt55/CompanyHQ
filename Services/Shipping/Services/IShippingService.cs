using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Shipping.Contracts.Dtos;
using CompanyHQ.Shipping.Contracts.Forms;

namespace CompanyHQ.Shipping.Services;

/// <summary>
/// Shipping methods, the origins they leave from, and the quote that picks between them. Anyone may
/// read active methods and ask for a quote; everything else needs <c>shipping.manage</c>, which the
/// service confirms from the role row itself.
/// </summary>
public interface IShippingService
{
    /// <summary>Active methods for anyone; every method, in the staff shape, for a manager who asks for it.</summary>
    Task<PagedResult<ClientShippingMethodDto>> ListMethodsAsync(
        PageRequest page,
        SearchRequest? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such method, or it is inactive and the caller is not a manager.</summary>
    Task<ClientShippingMethodDto?> GetMethodAsync(
        Guid methodId,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    /// <exception cref="UnauthorizedAccessException">The caller's role does not grant <c>shipping.manage</c>.</exception>
    /// <exception cref="ArgumentException">A rule pattern is malformed, or a named origin does not exist.</exception>
    Task<AdminShippingMethodDto> CreateMethodAsync(
        SaveShippingMethodRequest request,
        Guid? actingUserId,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CreateMethodAsync"/>
    Task<AdminShippingMethodDto?> UpdateMethodAsync(
        Guid methodId,
        SaveShippingMethodRequest request,
        Guid? actingUserId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteMethodAsync(Guid methodId, Guid? actingUserId, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminFulfillmentOriginDto>> ListOriginsAsync(
        PageRequest page,
        SearchRequest? search,
        Guid? actingUserId,
        CancellationToken cancellationToken = default);

    Task<AdminFulfillmentOriginDto?> GetOriginAsync(Guid originId, Guid? actingUserId, CancellationToken cancellationToken = default);

    Task<AdminFulfillmentOriginDto> CreateOriginAsync(
        SaveFulfillmentOriginRequest request,
        Guid? actingUserId,
        CancellationToken cancellationToken = default);

    Task<AdminFulfillmentOriginDto?> UpdateOriginAsync(
        Guid originId,
        SaveFulfillmentOriginRequest request,
        Guid? actingUserId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteOriginAsync(Guid originId, Guid? actingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cheapest plan that ships every line to the address. An unshippable basket is an answer,
    /// not an error: <see cref="ShippingQuoteDto.IsAvailable"/> is false and the failures say why.
    /// </summary>
    Task<ShippingQuoteDto> QuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken = default);
}
