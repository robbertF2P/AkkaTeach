namespace AkkaTeach.Contracts;

/// <summary>
/// Response to <see cref="CountMessageCommand"/>.
/// </summary>
public sealed record HandledCountResponse(int Count) : IActorSystemMessage;
