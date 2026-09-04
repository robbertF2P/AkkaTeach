namespace AkkaTeach.Contracts;

/// <summary>
/// Reply after a worker has processed one record.
/// </summary>
public sealed record DataRecordProcessed(string RecordId, int ProcessedValue) : IActorSystemMessage;
