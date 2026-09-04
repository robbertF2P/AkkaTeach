namespace AkkaTeach.Contracts;

/// <summary>
/// Published when a session transitions from idle to active.
/// </summary>
public sealed record SessionStarted(string SessionId) : IActorSystemEvent;
