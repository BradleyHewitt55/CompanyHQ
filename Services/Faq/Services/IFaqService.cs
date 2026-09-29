using CompanyHQ.Services.Faqs.Contracts.Dtos;
using CompanyHQ.Services.Faqs.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Enums;

namespace CompanyHQ.Services.Faqs.Services;

public interface IFaqService
{
    Task<PagedResult<ClientFaqEntryDto>> ListAsync(
        PageRequest page,
        string? search = null,
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
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<ClientFaqEntryDto?> UpdateAsync(
        string key,
        UpdateFaqEntryRequest request,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string key,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);
}
