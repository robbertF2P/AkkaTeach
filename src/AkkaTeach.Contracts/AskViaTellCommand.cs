namespace AkkaTeach.Contracts;

/// <summary>
/// Ask the front desk to greet someone using <c>Tell(target, message, sender)</c>.
/// </summary>
public sealed record AskViaTellCommand(string Name) : IActorSystemMessage;
