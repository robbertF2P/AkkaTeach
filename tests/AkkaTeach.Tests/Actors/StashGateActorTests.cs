using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Actors;

public sealed class StashGateActorTests : TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
    }

    [Fact]
    public void ProcessItems_BeforeGateReady_AreStashedThenHandledInOrder()
    {
        var gate = Sys.ActorOf(StashGateActor.Props(), "stash-gate");
        var replyProbe = CreateTestProbe();
        var eventProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(StashGateOpened));

        gate.Tell(new PrepareStashGateCommand("orders", TimeSpan.FromMilliseconds(300)));

        gate.Tell(new ProcessGatedItemCommand("a"), replyProbe.Ref);
        gate.Tell(new ProcessGatedItemCommand("b"), replyProbe.Ref);
        gate.Tell(new ProcessGatedItemCommand("c"), replyProbe.Ref);

        var statusProbe = CreateTestProbe();
        gate.Tell(new GetStashGateStatusQuery(), statusProbe.Ref);
        statusProbe.ExpectMsg<StashGateStatusResponse>(msg =>
        {
            msg.State.Should().Be("Waiting");
            msg.StashedCount.Should().Be(3);
            return true;
        });

        eventProbe.ExpectMsg<StashGateOpened>(msg =>
        {
            msg.GateId.Should().Be("orders");
            msg.StashedCount.Should().Be(3);
            return true;
        });

        replyProbe.ExpectMsg<GatedItemProcessed>(msg => msg.ItemId == "a");
        replyProbe.ExpectMsg<GatedItemProcessed>(msg => msg.ItemId == "b");
        replyProbe.ExpectMsg<GatedItemProcessed>(msg => msg.ItemId == "c");
    }

    [Fact]
    public void ProcessItems_AfterGateReady_AreNotStashed()
    {
        var gate = Sys.ActorOf(StashGateActor.Props(), "stash-gate-ready");
        var replyProbe = CreateTestProbe();
        var eventProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(StashGateOpened));

        gate.Tell(new PrepareStashGateCommand("fast", TimeSpan.Zero));
        eventProbe.ExpectMsg<StashGateOpened>(msg => msg.GateId == "fast");

        gate.Tell(new ProcessGatedItemCommand("x"), replyProbe.Ref);
        replyProbe.ExpectMsg<GatedItemProcessed>(msg =>
        {
            msg.ItemId.Should().Be("x");
            msg.Result.Should().Contain("fast");
            return true;
        });
    }
}
