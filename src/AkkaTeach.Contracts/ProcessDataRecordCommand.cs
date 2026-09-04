namespace AkkaTeach.Contracts;

/// <summary>
/// Command routed to a worker in the pool for a single record.
/// </summary>
public sealed record ProcessDataRecordCommand(ExternalDataRecord Record) : IActorSystemMessage;
