namespace AkkaTeach.Contracts;

/// <summary>
/// Reports which lifecycle hook an actor just ran.
/// </summary>
public sealed record LifecycleSignal(string Hook, string ActorName) : IActorSystemMessage;
