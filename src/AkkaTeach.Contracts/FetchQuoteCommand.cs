namespace AkkaTeach.Contracts;

/// <summary>
/// Starts an async fetch. The actor uses <c>PipeTo</c> so the mailbox is not blocked while waiting.
/// </summary>
public sealed record FetchQuoteCommand(string Topic) : IActorSystemMessage;
