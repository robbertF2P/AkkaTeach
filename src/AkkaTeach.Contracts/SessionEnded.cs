namespace AkkaTeach.Contracts;

/// <summary>
/// Published when a session transitions from active to completed.
/// </summary>
public sealed record SessionEnded(string SessionId, int TotalSteps) : IActorSystemEvent;
