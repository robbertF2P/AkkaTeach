namespace AkkaTeach.Contracts;

/// <summary>
/// Makes the receiving actor throw, so supervision and restart behaviour can be observed.
/// </summary>
public sealed record BoomCommand : IActorSystemMessage;
