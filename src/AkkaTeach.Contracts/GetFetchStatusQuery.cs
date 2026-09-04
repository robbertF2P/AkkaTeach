namespace AkkaTeach.Contracts;

/// <summary>
/// Query whether the actor is idle or currently waiting on an async call.
/// </summary>
public sealed record GetFetchStatusQuery : IActorSystemMessage;
