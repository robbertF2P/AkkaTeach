namespace AkkaTeach.Contracts;

/// <summary>
/// Published when every record on a page has been processed.
/// </summary>
public sealed record DataPageProcessed(int PageNumber, int RecordCount) : IActorSystemEvent;
