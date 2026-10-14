using CompanyHQ.Moderation.Contracts.Dtos;
using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Moderation.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.Moderation.Services;

public interface IContentHider
{
    Task<IReadOnlySet<Guid>> HiddenIdsAsync(
        Guid userId,
        string targetKind,
        CancellationToken ct = default);

    Task HideAsync(
        Guid userId,
        HideContentRequest request,
        HiddenContentSource source = HiddenContentSource.Manual,
        Guid? sourceId = null,
        CancellationToken ct = default);

    Task<bool> RemoveSourceAsync(
        Guid userId,
        string targetKind,
        Guid targetId,
        HiddenContentSource source,
        Guid sourceId,
        CancellationToken ct = default);

    Task<bool> UnhideAsync(
        Guid userId,
        string targetKind,
        Guid targetId,
        CancellationToken ct = default);

    Task<PagedResult<ClientHiddenContentDto>> ListAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken ct = default);
}
