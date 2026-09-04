namespace AkkaTeach.Contracts;

/// <summary>
/// Reply from the greeter.
/// </summary>
public sealed record HelloReply(string Message) : IActorSystemMessage;
