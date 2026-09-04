namespace AkkaTeach.Contracts;

/// <summary>
/// Message sent to the greeter actor.
/// </summary>
public sealed record SayHelloCommand(string Name) : IActorSystemMessage;
