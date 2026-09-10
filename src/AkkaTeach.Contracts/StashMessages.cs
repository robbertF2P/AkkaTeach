namespace AkkaTeach.Contracts;

/// <summary>
/// Opens the gate and starts a dependency warm-up period. Work received before the gate
/// is ready is stashed.
/// </summary>
public sealed record PrepareStashGateCommand(string GateId, TimeSpan InitDelay) : IActorSystemMessage;

/// <summary>
/// Work item processed only after the gate is ready; stashed otherwise.
/// </summary>
public sealed record ProcessGatedItemCommand(string ItemId) : IActorSystemMessage;

/// <summary>
/// Reply after a gated item is handled.
/// </summary>
public sealed record GatedItemProcessed(string ItemId, string Result) : IActorSystemMessage;

/// <summary>
/// Query gate state and stash depth.
/// </summary>
public sealed record GetStashGateStatusQuery : IActorSystemMessage;

/// <summary>
/// <see cref="State"/> is <c>Waiting</c> or <c>Ready</c>.
/// </summary>
public sealed record StashGateStatusResponse(
    string State,
    string? GateId,
    int StashedCount,
    int ProcessedCount) : IActorSystemMessage;

/// <summary>
/// Published when the gate opens and stashed work is unstashed.
/// </summary>
public sealed record StashGateOpened(string GateId, int StashedCount) : IActorSystemEvent;
