namespace AkkaTeach.Contracts;

/// <summary>
/// Ends the current session. Only accepted while active.
/// </summary>
public sealed record EndSessionCommand : IActorSystemMessage;
