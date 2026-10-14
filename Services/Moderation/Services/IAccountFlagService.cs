using CompanyHQ.Moderation.Contracts.Dtos;
using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Moderation.Contracts.Forms;
using CompanyHQ.Moderation.Contracts.Ports;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.Moderation.Services;

/// <summary>Flag review for the HTTP routes; the raise-and-read other services use is <see cref="IAccountFlags"/>.</summary>
public interface IAccountFlagService : IAccountFlags
{
    Task<PagedResult<ClientAccountFlagDto>> ListAdminAsync(
        PageRequest paging,
        AccountFlagStatus? status = null,
        string? kind = null,
        SearchRequest? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default);

    Task<bool> DecideAsync(
        Guid actingAdministratorId,
        Guid flagId,
        FlagDecisionRequest request,
        CancellationToken ct = default);

    Task<PagedResult<ClientAccountFlagDto>> ListForUserAsync(
        Guid userId,
        PageRequest paging,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default);
}
