namespace CompanyHQ.Services.Messages.Contracts.Forms;

public sealed record SendMessageRequest
{
    public SendMessageRequest(Guid clientMessageId, string body, Guid? mediaObjectId = null)
    {
        if (clientMessageId == Guid.Empty)
        {
            throw new ArgumentException("ClientMessageId is required.", nameof(clientMessageId));
        }

        if (mediaObjectId == Guid.Empty)
        {
            throw new ArgumentException("MediaObjectId must be a non-empty identifier.", nameof(mediaObjectId));
        }

        body = body?.Trim() ?? string.Empty;
        if (body.Length == 0 && mediaObjectId is null)
        {
            throw new ArgumentException("Body or MediaObjectId is required.", nameof(body));
        }

        if (body.Length > 4000)
        {
            throw new ArgumentException("Body cannot exceed 4000 characters.", nameof(body));
        }

        ClientMessageId = clientMessageId;
        Body = body;
        MediaObjectId = mediaObjectId;
    }

    public Guid ClientMessageId { get; }

    public string Body { get; }

    public Guid? MediaObjectId { get; }
}
