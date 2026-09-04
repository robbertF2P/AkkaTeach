namespace AkkaTeach.Contracts;

/// <summary>
/// Query for how many items the coordinator has completed.
/// </summary>
public sealed record GetCompletedCountQuery : IActorSystemMessage;
