namespace CompanyHQ.Services.Cloudflare.Contracts.Models;

/// <summary>Cloudflare R2 credentials and bucket settings owned by the Cloudflare adapter.</summary>
public sealed class R2Options
{
    public const string SectionName = "Cloudflare:R2";

    public string AccountId { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public Uri ServiceUrl => new($"https://{AccountId}.r2.cloudflarestorage.com");

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AccountId)) throw new InvalidOperationException("Cloudflare:R2:AccountId is required.");
        if (string.IsNullOrWhiteSpace(AccessKeyId)) throw new InvalidOperationException("Cloudflare:R2:AccessKeyId is required.");
        if (string.IsNullOrWhiteSpace(SecretAccessKey)) throw new InvalidOperationException("Cloudflare:R2:SecretAccessKey is required.");
        if (string.IsNullOrWhiteSpace(Bucket)) throw new InvalidOperationException("Cloudflare:R2:Bucket is required.");
    }
}
