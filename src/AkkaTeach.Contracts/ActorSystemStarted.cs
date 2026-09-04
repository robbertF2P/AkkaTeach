namespace AkkaTeach.Contracts;

/// <summary>
/// Published when a top-level actor has started.
/// </summary>
public sealed record ActorSystemStarted(string ActorName) : IActorSystemEvent;
