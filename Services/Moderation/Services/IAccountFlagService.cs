using CompanyHQ.Services.Moderation.Contracts.Dtos;
using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Forms;
using CompanyHQ.Services.Moderation.Contracts.Ports;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Moderation.Services;

/// <summary>Flag review for the HTTP routes; the raise-and-read other services use is <see cref="IAccountFlags"/>.</summary>
public interface IAccountFlagService : IAccountFlags
{
    Task<PagedResult<ClientAccountFlagDto>> ListAdminAsync(
        PageRequest paging,
        AccountFlagStatus? status = null,
        string? kind = null,
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
