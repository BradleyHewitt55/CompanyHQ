using CompanyHQ.Services.Moderation.Contracts.Dtos;
using CompanyHQ.Services.Moderation.Contracts.Forms;
using CompanyHQ.Services.Moderation.Contracts.Ports;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Moderation.Services;

/// <summary>Block management for the HTTP routes; the reads other services use are <see cref="IBlockChecker"/>.</summary>
public interface IBlockService : IBlockChecker
{
    Task<BlockResult> BlockAsync(
        Guid blockerUserId,
        BlockUserRequest request,
        CancellationToken ct = default);

    Task<bool> UnblockAsync(Guid blockerUserId, Guid blockedUserId, CancellationToken ct = default);

    Task<PagedResult<ClientUserBlockDto>> ListOutboundAsync(
        Guid blockerUserId,
        PageRequest paging,
        CancellationToken ct = default);
}
