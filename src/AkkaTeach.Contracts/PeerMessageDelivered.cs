namespace AkkaTeach.Contracts;

/// <summary>
/// Published when a peer has received a direct message from another peer.
/// </summary>
public sealed record PeerMessageDelivered(string To, string From, string Text) : IActorSystemEvent;
