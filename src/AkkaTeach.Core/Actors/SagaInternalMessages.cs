namespace AkkaTeach.Core.Actors;

internal sealed record SagaReserveInventory(string OrderId);

internal sealed record SagaInventoryReserved(string OrderId);

internal sealed record SagaChargePayment(string OrderId, decimal Amount);

internal sealed record SagaPaymentCharged(string OrderId);

internal sealed record SagaShipOrder(string OrderId);

internal sealed record SagaOrderShipped(string OrderId);

internal sealed record SagaStepFailed(string OrderId, string Step, string Reason);

internal sealed record SagaReleaseInventory(string OrderId);

internal sealed record SagaInventoryReleased(string OrderId);

internal sealed record SagaRefundPayment(string OrderId, decimal Amount);

internal sealed record SagaPaymentRefunded(string OrderId);
