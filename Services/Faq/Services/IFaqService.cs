using CompanyHQ.Faqs.Contracts.Dtos;
using CompanyHQ.Faqs.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Enums;

namespace CompanyHQ.Faqs.Services;

public interface IFaqService
{
    Task<PagedResult<ClientFaqEntryDto>> ListAsync(
        PageRequest page,
        SearchRequest? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<ClientFaqEntryDto?> GetAsync(
        string key,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<ClientFaqEntryDto> CreateAsync(
        CreateFaqEntryRequest request,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<ClientFaqEntryDto?> UpdateAsync(
        string key,
        UpdateFaqEntryRequest request,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string key,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);
}
