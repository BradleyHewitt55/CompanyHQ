using System.Text.Json;

namespace CompanyHQ.Services.Messages.Services;

internal static class MessageCursor
{
    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new CursorValue(createdAt, id));
        return Convert.ToBase64String(json)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static bool TryDecode(string? value, out (DateTimeOffset CreatedAt, Guid Id)? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(padded);
            var decoded = JsonSerializer.Deserialize<CursorValue>(bytes);
            if (decoded is null || decoded.Id == Guid.Empty)
            {
                return false;
            }

            cursor = (decoded.CreatedAt, decoded.Id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record CursorValue(DateTimeOffset CreatedAt, Guid Id);
}
