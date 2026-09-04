namespace AkkaTeach.Contracts;

/// <summary>
/// Asks a supervisor to create a supervised child with the given name.
/// </summary>
public sealed record SpawnChildCommand(string Name) : IActorSystemMessage;
