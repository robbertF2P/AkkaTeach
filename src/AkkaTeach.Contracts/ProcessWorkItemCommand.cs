namespace AkkaTeach.Contracts;

/// <summary>
/// Command to process a work item. The coordinator forwards this to a child worker.
/// </summary>
public sealed record ProcessWorkItemCommand(string ItemId, int Payload) : IActorSystemMessage;
