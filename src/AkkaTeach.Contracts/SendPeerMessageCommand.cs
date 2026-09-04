namespace AkkaTeach.Contracts;

/// <summary>
/// Send a message to a previously introduced peer by name.
/// </summary>
public sealed record SendPeerMessageCommand(string TargetName, string Text) : IActorSystemMessage;
