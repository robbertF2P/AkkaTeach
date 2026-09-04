namespace AkkaTeach.Contracts;

/// <summary>
/// Current ingestion state and counters.
/// </summary>
public sealed record IngestionStatusResponse(
    string State,
    int PagesCollected,
    int RecordsProcessed,
    int TotalRecords) : IActorSystemMessage;
