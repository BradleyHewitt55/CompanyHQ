using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Media.Contracts.Enums;
using CompanyHQ.Services.Media.Contracts.Ports;

namespace Northstar.Demo;

// Mailpit accepts real SMTP messages locally; the production Email outbox still owns retries.
public sealed class LocalEmailProvider(IConfiguration configuration) : IEmailProvider
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        message.Validate();
        using var mail = new MailMessage { From = new("hello@northstar.test", configuration["Demo:CompanyName"] ?? "Demo Company"), Subject = message.Subject, Body = message.Text };
        foreach (var address in message.To) mail.To.Add(address);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, Encoding.UTF8, "text/html"));
        using var smtp = new SmtpClient(configuration["Demo:SmtpHost"] ?? "localhost", configuration.GetValue("Demo:SmtpPort", 51025));
        try
        {
            await smtp.SendMailAsync(mail, cancellationToken);
            return EmailSendResult.Sent(Guid.NewGuid().ToString("N"));
        }
        catch (SmtpException) { return EmailSendResult.Failed(true, "Local SMTP delivery failed."); }
    }
}

// A demo-only provider. Capability URLs bind method, key, type, and expiry; Message bytes stay private.
public sealed class LocalObjectStorage : IMediaObjectStorageProvider
{
    private readonly string root;
    private readonly byte[] signingKey;
    private readonly string origin;

    public LocalObjectStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../.local/storage"));
        Directory.CreateDirectory(root);
        signingKey = Convert.FromBase64String(configuration["Demo:StorageSigningKey"] ?? throw new InvalidOperationException("Missing demo storage signing key."));
        origin = configuration["Media:ContentBaseUrl"] ?? "http://localhost:4102";
    }

    public string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid object key.");
        return path;
    }

    private byte[] Sign(string method, string key, string type, long expires) =>
        HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes($"{method}\n{key}\n{type}\n{expires}"));

    public string CreateUrl(string method, string key, string type, DateTimeOffset expires)
    {
        Resolve(key);
        var stamp = expires.ToUnixTimeSeconds();
        var signature = Convert.ToHexString(Sign(method, key, type, stamp));
        return $"{origin}/demo/storage/{key}?expires={stamp}&type={Uri.EscapeDataString(type)}&signature={signature}";
    }

    public bool Authorize(string method, string key, string type, long expires, string signature)
    {
        if (expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
        try { Resolve(key); return CryptographicOperations.FixedTimeEquals(Sign(method, key, type, expires), Convert.FromHexString(signature)); }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return false; }
    }

    public string CreateUploadUrl(string key, string type, DateTimeOffset expires) => CreateUrl("PUT", key, type, expires);
    public string CreateDownloadUrl(string key, DateTimeOffset expires) => CreateUrl("GET", key, "", expires);

    public async Task<bool> PutAsync(string key, string type, Stream content, long length, CancellationToken ct = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".upload";
        try
        {
            await using (var file = File.Create(temporary))
            {
                await content.CopyToAsync(file, ct);
                if (file.Length != length) return false;
            }
            File.Move(temporary, path, overwrite: true);
            await File.WriteAllTextAsync(path + ".json", JsonSerializer.Serialize(new StoredObjectMetadata(type, length)), ct);
            return true;
        }
        finally { File.Delete(temporary); }
    }

    public async Task<StoredObjectMetadata?> GetMetadataAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        if (!File.Exists(path) || !File.Exists(path + ".json")) return null;
        var metadata = JsonSerializer.Deserialize<StoredObjectMetadata>(await File.ReadAllTextAsync(path + ".json", ct));
        return metadata is null ? null : metadata with { SizeBytes = new FileInfo(path).Length };
    }

    public Task<StorageDeleteOutcome> DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        File.Delete(path);
        File.Delete(path + ".json");
        return Task.FromResult(StorageDeleteOutcome.AlreadyGone);
    }
}

public static class LocalStorageEndpoints
{
    public static void MapLocalObjectStorage(this WebApplication app)
    {
        app.MapMethods("/demo/storage/{**key}", ["GET", "PUT"], async (HttpContext context, string key, LocalObjectStorage storage) =>
        {
            var query = context.Request.Query;
            var type = query["type"].ToString();
            if (!long.TryParse(query["expires"], out var expires) || !storage.Authorize(context.Request.Method, key, type, expires, query["signature"].ToString())) return Results.Unauthorized();
            if (context.Request.Method == "PUT")
            {
                if (context.Request.ContentLength is not { } length || length is <= 0 or > 25_000_000 || context.Request.ContentType != type) return Results.BadRequest();
                return await storage.PutAsync(key, type, context.Request.Body, length, context.RequestAborted) ? Results.NoContent() : Results.BadRequest();
            }
            var metadata = await storage.GetMetadataAsync(key, context.RequestAborted);
            return metadata is null ? Results.NotFound() : Results.File(storage.Resolve(key), metadata.ContentType);
        }).AllowAnonymous();
    }
}

