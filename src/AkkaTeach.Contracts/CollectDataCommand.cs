namespace AkkaTeach.Contracts;

/// <summary>
/// Starts collecting and processing all pages from the API client.
/// </summary>
public sealed record CollectDataCommand(string? CollectionId = null) : IActorSystemMessage;
