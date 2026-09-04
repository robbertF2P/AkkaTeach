namespace AkkaTeach.Contracts;

/// <summary>
/// Published when the coordinator has completed a work item.
/// </summary>
public sealed record WorkItemCompleted(string ItemId, int Result) : IActorSystemEvent;
