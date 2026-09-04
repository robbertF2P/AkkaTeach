namespace AkkaTeach.Contracts;

/// <summary>
/// Delivered when another peer sends a chat message.
/// </summary>
public sealed record PeerMessageReceived(string From, string Text) : IActorSystemMessage;
