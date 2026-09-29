namespace CompanyHQ.Services.Messages.Contracts.Ports;

/// <summary>One notification Message wants delivered; <see cref="Data"/> carries routing ids only.</summary>
public sealed record MessageNotification(
    Guid RecipientUserId,
    string Kind,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);
