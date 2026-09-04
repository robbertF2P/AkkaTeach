namespace AkkaTeach.Contracts;

/// <summary>
/// Ask the front desk to greet someone using <c>Forward(message)</c>.
/// </summary>
public sealed record AskViaForwardCommand(string Name) : IActorSystemMessage;
