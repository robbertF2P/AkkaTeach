namespace AkkaTeach.Contracts;

/// <summary>
/// Resets a completed session back to idle so a new session can begin.
/// </summary>
public sealed record ResetSessionCommand : IActorSystemMessage;
