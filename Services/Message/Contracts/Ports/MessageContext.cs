namespace CompanyHQ.Services.Messages.Contracts.Ports;

/// <summary>A message and its immediate neighbours, frozen as evidence for a report or a flag.</summary>
public sealed record MessageContext(
    Guid SenderUserId,
    IReadOnlyList<MessageCapture> Captures);

/// <summary>One message inside a <see cref="MessageContext"/>.</summary>
public sealed record MessageCapture(
    Guid AuthorUserId,
    string Body,
    DateTimeOffset OccurredAt,
    bool IsTarget);
