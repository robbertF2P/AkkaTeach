namespace AkkaTeach.Contracts;

/// <summary>
/// <c>Idle</c> or <c>Fetching</c>.
/// </summary>
public sealed record FetchStatusResponse(string State) : IActorSystemMessage;
