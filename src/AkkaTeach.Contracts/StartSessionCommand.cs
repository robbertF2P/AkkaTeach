namespace AkkaTeach.Contracts;

/// <summary>
/// Starts a teaching session. Only accepted while the session actor is idle.
/// </summary>
public sealed record StartSessionCommand(string SessionId) : IActorSystemMessage;
