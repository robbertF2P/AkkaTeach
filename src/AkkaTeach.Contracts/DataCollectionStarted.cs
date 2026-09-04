namespace AkkaTeach.Contracts;

/// <summary>
/// Published when the first page of a collection has been received.
/// </summary>
public sealed record DataCollectionStarted(string CollectionId, int TotalPages, int ExpectedRecords) : IActorSystemEvent;
