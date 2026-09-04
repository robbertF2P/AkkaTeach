namespace AkkaTeach.Contracts;

/// <summary>
/// Reply from a worker after processing a single item.
/// </summary>
public sealed record WorkItemProcessed(string ItemId, int Result) : IActorSystemMessage;
