namespace AkkaTeach.Contracts;

/// <summary>
/// Simulated failure point for teaching compensations.
/// </summary>
public enum SagaFailurePoint
{
    None,
    ReserveInventory,
    ChargePayment,
    ShipOrder,
}

/// <summary>
/// Starts a multi-step order saga: reserve → charge → ship.
/// </summary>
public sealed record StartOrderSagaCommand(
    string OrderId,
    decimal Amount,
    SagaFailurePoint FailAt = SagaFailurePoint.None) : IActorSystemMessage;

/// <summary>
/// Reply when all saga steps succeed.
/// </summary>
public sealed record OrderSagaCompleted(string OrderId) : IActorSystemMessage;

/// <summary>
/// Reply when a step fails after compensation.
/// </summary>
public sealed record OrderSagaFailed(string OrderId, string FailedStep, string Reason) : IActorSystemMessage;

/// <summary>
/// Published after each successful forward step.
/// </summary>
public sealed record OrderSagaStepCompleted(string OrderId, string Step) : IActorSystemEvent;

/// <summary>
/// Published when a compensating action runs.
/// </summary>
public sealed record OrderSagaCompensationApplied(string OrderId, string Action) : IActorSystemEvent;
