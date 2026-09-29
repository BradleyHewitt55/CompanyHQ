namespace CompanyHQ.Services.Messages.Contracts.Dtos;

/// <summary>Result of a send, including whether this was a retry of an existing client id.</summary>
public sealed record SendMessageResult(ClientMessageDto Message, bool WasExisting);
