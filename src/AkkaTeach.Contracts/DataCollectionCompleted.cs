namespace AkkaTeach.Contracts;

/// <summary>
/// Published when all pages in a collection have been processed.
/// </summary>
public sealed record DataCollectionCompleted(string CollectionId, int TotalRecords, long ElapsedMilliseconds) : IActorSystemEvent;
