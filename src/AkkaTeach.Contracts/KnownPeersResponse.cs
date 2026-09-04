namespace AkkaTeach.Contracts;

/// <summary>
/// Response to <see cref="GetKnownPeersQuery"/>.
/// </summary>
public sealed record KnownPeersResponse(IReadOnlyList<string> PeerNames) : IActorSystemMessage;
