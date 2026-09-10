namespace AkkaTeach.Contracts;

/// <summary>
/// Ask the manager to spawn a short-lived worker that performs one risky operation.
/// </summary>
public sealed record RunKamikazeTaskCommand(string TaskId, bool ShouldFail = false) : IActorSystemMessage;

/// <summary>
/// Reply when the kamikaze worker completed and self-terminated successfully.
/// </summary>
public sealed record KamikazeTaskSucceeded(string TaskId, string Result) : IActorSystemMessage;

/// <summary>
/// Reply when the worker crashed or stopped before reporting success.
/// </summary>
public sealed record KamikazeTaskFailed(string TaskId, string Reason) : IActorSystemMessage;

/// <summary>
/// Published when a kamikaze task completes (success or failure).
/// </summary>
public sealed record KamikazeTaskFinished(string TaskId, bool Succeeded, string Detail) : IActorSystemEvent;
