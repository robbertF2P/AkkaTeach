namespace AkkaTeach.Contracts;

/// <summary>
/// Records progress during an active session.
/// </summary>
public sealed record RecordProgressCommand(int Step) : IActorSystemMessage;
