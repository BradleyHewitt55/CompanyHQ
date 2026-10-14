namespace CompanyHQ.Media.Contracts.Ports;

/// <summary>Metadata observed from an object store, independent of its provider.</summary>
public sealed record StoredObjectMetadata(string ContentType, long SizeBytes);
