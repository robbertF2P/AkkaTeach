namespace AkkaTeach.Contracts;

/// <summary>
/// Response describing the session actor's current state.
/// </summary>
public sealed record SessionStateResponse(string State, string? SessionId, int StepsRecorded) : IActorSystemMessage;
