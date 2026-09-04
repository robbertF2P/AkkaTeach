namespace AkkaTeach.Contracts;

/// <summary>
/// Reply after a quote has been fetched.
/// </summary>
public sealed record QuoteFetchedResponse(string Topic, string Quote) : IActorSystemMessage;
