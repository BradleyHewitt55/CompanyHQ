using CompanyHQ.Services.Moderation.Contracts.Dtos;
using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Moderation.Services;

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
