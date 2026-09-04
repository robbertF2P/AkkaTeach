namespace AkkaTeach.Contracts;

/// <summary>
/// Increments the receiving actor's in-memory counter, used to show that a restart clears state.
/// </summary>
public sealed record CountMessageCommand : IActorSystemMessage;
