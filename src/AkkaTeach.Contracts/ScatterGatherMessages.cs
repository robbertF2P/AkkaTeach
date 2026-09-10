namespace AkkaTeach.Contracts;

/// <summary>
/// Fan out multiple work items and aggregate all replies into one response.
/// </summary>
public sealed record ScatterGatherCommand(
    string BatchId,
    IReadOnlyList<ProcessWorkItemCommand> Items) : IActorSystemMessage;

/// <summary>
/// Aggregated reply after every worker has responded.
/// </summary>
public sealed record ScatterGatherCompleted(
    string BatchId,
    IReadOnlyList<WorkItemProcessed> Results) : IActorSystemMessage;

/// <summary>
/// Query whether the coordinator is idle or still waiting for worker replies.
/// </summary>
public sealed record ScatterGatherStatusQuery : IActorSystemMessage;

/// <summary>
/// <see cref="ScatterGatherStatusResponse.State"/> is <c>Idle</c> or <c>Aggregating</c>.
/// </summary>
public sealed record ScatterGatherStatusResponse(string State, int Received, int Expected) : IActorSystemMessage;

/// <summary>
/// Published when a scatter-gather batch finishes.
/// </summary>
public sealed record ScatterGatherFinished(string BatchId, int ResultCount) : IActorSystemEvent;
