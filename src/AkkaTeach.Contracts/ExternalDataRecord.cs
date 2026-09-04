namespace AkkaTeach.Contracts;

/// <summary>
/// A single record returned by the external data API.
/// </summary>
public sealed record ExternalDataRecord(string Id, int Value, string Source);
