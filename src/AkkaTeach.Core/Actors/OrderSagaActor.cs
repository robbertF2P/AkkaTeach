using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Process manager for a three-step order flow with compensating transactions.
/// </summary>
/// <remarks>
/// <para><b>Saga pattern:</b> long-running workflow across steps. Each forward step uses
/// <c>Become</c> to wait for the next message. On failure, run compensations in reverse
/// order for steps already completed.</para>
/// <para>See guide §19.</para>
/// </remarks>
public sealed class OrderSagaActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private IActorRef? _requester;
    private string _orderId = string.Empty;
    private decimal _amount;
    private SagaFailurePoint _failAt;
    private bool _inventoryReserved;
    private bool _paymentCharged;
    private string _failedStep = string.Empty;
    private string _failedReason = string.Empty;

    public OrderSagaActor()
    {
        Become(Idle);
    }

    private void Idle()
    {
        Receive<StartOrderSagaCommand>(command =>
        {
            _requester = Sender.IsNobody() ? null : Sender;
            _orderId = command.OrderId;
            _amount = command.Amount;
            _failAt = command.FailAt;
            _inventoryReserved = false;
            _paymentCharged = false;

            _log.Info("Starting order saga for {OrderId}", _orderId);
            Self.Tell(new SagaReserveInventory(_orderId));
            Become(Reserving);
        });
    }

    private void Reserving()
    {
        Receive<SagaReserveInventory>(command => ExecuteForwardStep(
            command.OrderId,
            "ReserveInventory",
            SagaFailurePoint.ReserveInventory,
            () => new SagaInventoryReserved(command.OrderId)));

        Receive<SagaInventoryReserved>(reserved =>
        {
            _inventoryReserved = true;
            PublishStep(reserved.OrderId, "ReserveInventory");
            Self.Tell(new SagaChargePayment(reserved.OrderId, _amount));
            Become(Charging);
        });

        Receive<SagaStepFailed>(BeginCompensation);
    }

    private void Charging()
    {
        Receive<SagaChargePayment>(command => ExecuteForwardStep(
            command.OrderId,
            "ChargePayment",
            SagaFailurePoint.ChargePayment,
            () => new SagaPaymentCharged(command.OrderId)));

        Receive<SagaPaymentCharged>(charged =>
        {
            _paymentCharged = true;
            PublishStep(charged.OrderId, "ChargePayment");
            Self.Tell(new SagaShipOrder(charged.OrderId));
            Become(Shipping);
        });

        Receive<SagaStepFailed>(BeginCompensation);
    }

    private void Shipping()
    {
        Receive<SagaShipOrder>(command => ExecuteForwardStep(
            command.OrderId,
            "ShipOrder",
            SagaFailurePoint.ShipOrder,
            () => new SagaOrderShipped(command.OrderId)));

        Receive<SagaOrderShipped>(shipped =>
        {
            PublishStep(shipped.OrderId, "ShipOrder");
            _requester?.Tell(new OrderSagaCompleted(shipped.OrderId));
            _log.Info("Order saga {OrderId} completed", shipped.OrderId);
            Become(Idle);
        });

        Receive<SagaStepFailed>(BeginCompensation);
    }

    private void Compensating()
    {
        Receive<SagaRefundPayment>(command =>
        {
            ApplyCompensation(command.OrderId, "RefundPayment");
            _paymentCharged = false;
            Self.Tell(new SagaPaymentRefunded(command.OrderId));
        });

        Receive<SagaPaymentRefunded>(_ =>
        {
            if (_inventoryReserved)
            {
                Self.Tell(new SagaReleaseInventory(_orderId));
                return;
            }

            FailSaga(_failedStep, _failedReason);
        });

        Receive<SagaReleaseInventory>(command =>
        {
            ApplyCompensation(command.OrderId, "ReleaseInventory");
            _inventoryReserved = false;
            Self.Tell(new SagaInventoryReleased(command.OrderId));
        });

        Receive<SagaInventoryReleased>(_ => FailSaga(_failedStep, _failedReason));
    }

    private void ExecuteForwardStep(
        string orderId,
        string stepName,
        SagaFailurePoint failurePoint,
        Func<object> successMessage)
    {
        if (_failAt == failurePoint)
        {
            Self.Tell(new SagaStepFailed(orderId, stepName, $"Simulated {stepName} failure"));
            return;
        }

        Self.Tell(successMessage());
    }

    private void BeginCompensation(SagaStepFailed failed)
    {
        _failedStep = failed.Step;
        _failedReason = failed.Reason;
        _log.Warning("Order saga {OrderId} failed at {Step}: {Reason}", failed.OrderId, failed.Step, failed.Reason);

        if (_paymentCharged)
        {
            Become(Compensating);
            Self.Tell(new SagaRefundPayment(_orderId, _amount));
            return;
        }

        if (_inventoryReserved)
        {
            Become(Compensating);
            Self.Tell(new SagaReleaseInventory(_orderId));
            return;
        }

        FailSaga(failed.Step, failed.Reason);
    }

    private void ApplyCompensation(string orderId, string action)
    {
        _log.Info("Compensating saga {OrderId}: {Action}", orderId, action);
        Context.System.EventStream.Publish(new OrderSagaCompensationApplied(orderId, action));
    }

    private void FailSaga(string step, string reason)
    {
        _requester?.Tell(new OrderSagaFailed(_orderId, step, reason));
        Become(Idle);
    }

    private void PublishStep(string orderId, string step) =>
        Context.System.EventStream.Publish(new OrderSagaStepCompleted(orderId, step));

    public static Props Props() => Akka.Actor.Props.Create<OrderSagaActor>();
}
