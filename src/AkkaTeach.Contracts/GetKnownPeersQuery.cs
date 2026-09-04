namespace AkkaTeach.Contracts;

/// <summary>
/// Lists peer names this actor knows how to reach.
/// </summary>
public sealed record GetKnownPeersQuery : IActorSystemMessage;
