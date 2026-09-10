using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Actors;

public sealed class OrderSagaActorTests : TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
    }

    [Fact]
    public void StartSaga_WhenAllStepsSucceed_CompletesInOrder()
    {
        var saga = Sys.ActorOf(OrderSagaActor.Props(), "order-saga");
        var replyProbe = CreateTestProbe();
        var stepProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(stepProbe.Ref, typeof(OrderSagaStepCompleted));

        saga.Tell(new StartOrderSagaCommand("order-1", 99.50m), replyProbe.Ref);

        stepProbe.ExpectMsg<OrderSagaStepCompleted>(msg => msg.Step == "ReserveInventory");
        stepProbe.ExpectMsg<OrderSagaStepCompleted>(msg => msg.Step == "ChargePayment");
        stepProbe.ExpectMsg<OrderSagaStepCompleted>(msg => msg.Step == "ShipOrder");

        replyProbe.ExpectMsg<OrderSagaCompleted>(msg => msg.OrderId == "order-1");
    }

    [Fact]
    public void StartSaga_WhenChargeFails_CompensatesInventoryRelease()
    {
        var saga = Sys.ActorOf(OrderSagaActor.Props(), "order-saga-charge-fail");
        var replyProbe = CreateTestProbe();
        var compensationProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(compensationProbe.Ref, typeof(OrderSagaCompensationApplied));

        saga.Tell(
            new StartOrderSagaCommand("order-2", 10m, SagaFailurePoint.ChargePayment),
            replyProbe.Ref);

        compensationProbe.ExpectMsg<OrderSagaCompensationApplied>(msg => msg.Action == "ReleaseInventory");
        replyProbe.ExpectMsg<OrderSagaFailed>(msg =>
        {
            msg.OrderId.Should().Be("order-2");
            msg.FailedStep.Should().Be("ChargePayment");
            return true;
        });
    }

    [Fact]
    public void StartSaga_WhenShipFails_RefundsThenReleasesInventory()
    {
        var saga = Sys.ActorOf(OrderSagaActor.Props(), "order-saga-ship-fail");
        var replyProbe = CreateTestProbe();
        var compensationProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(compensationProbe.Ref, typeof(OrderSagaCompensationApplied));

        saga.Tell(
            new StartOrderSagaCommand("order-3", 25m, SagaFailurePoint.ShipOrder),
            replyProbe.Ref);

        compensationProbe.ExpectMsg<OrderSagaCompensationApplied>(msg => msg.Action == "RefundPayment");
        compensationProbe.ExpectMsg<OrderSagaCompensationApplied>(msg => msg.Action == "ReleaseInventory");

        replyProbe.ExpectMsg<OrderSagaFailed>(msg => msg.FailedStep == "ShipOrder");
    }

    [Fact]
    public void StartSaga_WhenReserveFails_FailsWithoutCompensation()
    {
        var saga = Sys.ActorOf(OrderSagaActor.Props(), "order-saga-reserve-fail");
        var replyProbe = CreateTestProbe();
        var compensationProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(compensationProbe.Ref, typeof(OrderSagaCompensationApplied));

        saga.Tell(
            new StartOrderSagaCommand("order-4", 5m, SagaFailurePoint.ReserveInventory),
            replyProbe.Ref);

        replyProbe.ExpectMsg<OrderSagaFailed>(msg => msg.FailedStep == "ReserveInventory");
        compensationProbe.ExpectNoMsg(TimeSpan.FromMilliseconds(200));
    }
}
