namespace AkkaTeach.Contracts;

/// <summary>
/// Response to <see cref="GetCompletedCountQuery"/>.
/// </summary>
public sealed record CompletedCountResponse(int Count) : IActorSystemMessage;
