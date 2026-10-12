using CompanyHQ.Services.Rss.Contracts.Dtos;

namespace CompanyHQ.Services.Rss.Contracts.Forms;

/// <summary>
/// One feed download. <see cref="Validators"/> are the pair returned by the previous fetch of this
/// same URL; leave them out for a first fetch or to force a full download.
/// </summary>
public sealed record FeedFetchRequest(string Url, FeedValidatorsDto? Validators = null);
