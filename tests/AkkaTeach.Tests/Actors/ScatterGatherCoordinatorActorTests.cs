using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Actors;

/// <summary>
/// Tests for fan-out / scatter-gather aggregation when the expected reply count is known.
/// </summary>
public sealed class ScatterGatherCoordinatorActorTests : TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
    }

    [Fact]
    public void ScatterGather_FansOutToWorkers_AggregatesAllReplies()
    {
        var coordinator = Sys.ActorOf(ScatterGatherCoordinatorActor.Props(), "scatter-gather");
        var replyProbe = CreateTestProbe();

        var items = new[]
        {
            new ProcessWorkItemCommand("a", 1),
            new ProcessWorkItemCommand("b", 2),
            new ProcessWorkItemCommand("c", 3),
        };

        coordinator.Tell(new ScatterGatherCommand("batch-1", items), replyProbe.Ref);

        replyProbe.ExpectMsg<ScatterGatherCompleted>(msg =>
        {
            msg.BatchId.Should().Be("batch-1");
            msg.Results.Should().HaveCount(3);
            msg.Results.Select(r => r.ItemId).Should().BeEquivalentTo("a", "b", "c");
            msg.Results.Sum(r => r.Result).Should().Be(12);
            return true;
        });
    }

    [Fact]
    public void ScatterGather_StatusQuery_ReturnsIdleWhenNotRunning()
    {
        var coordinator = Sys.ActorOf(ScatterGatherCoordinatorActor.Props(), "scatter-gather-idle-status");
        var statusProbe = CreateTestProbe();

        coordinator.Tell(new ScatterGatherStatusQuery(), statusProbe.Ref);

        statusProbe.ExpectMsg<ScatterGatherStatusResponse>(msg =>
        {
            msg.State.Should().Be("Idle");
            msg.Received.Should().Be(0);
            msg.Expected.Should().Be(0);
            return true;
        });
    }

    [Fact]
    public void ScatterGather_AfterCompletion_StatusReturnsIdleAgain()
    {
        var coordinator = Sys.ActorOf(ScatterGatherCoordinatorActor.Props(), "scatter-gather-idle-after");
        var replyProbe = CreateTestProbe();
        var statusProbe = CreateTestProbe();

        coordinator.Tell(
            new ScatterGatherCommand("done", [new ProcessWorkItemCommand("z", 1)]),
            replyProbe.Ref);

        replyProbe.ExpectMsg<ScatterGatherCompleted>();

        coordinator.Tell(new ScatterGatherStatusQuery(), statusProbe.Ref);
        statusProbe.ExpectMsg<ScatterGatherStatusResponse>(msg => msg.State == "Idle");
    }

    [Fact]
    public void ScatterGather_WhenComplete_PublishesFinishedEvent()
    {
        var coordinator = Sys.ActorOf(ScatterGatherCoordinatorActor.Props(), "scatter-gather-event");
        var replyProbe = CreateTestProbe();
        var eventProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(ScatterGatherFinished));

        coordinator.Tell(
            new ScatterGatherCommand("batch-evt", [new ProcessWorkItemCommand("x", 4)]),
            replyProbe.Ref);

        replyProbe.ExpectMsg<ScatterGatherCompleted>();
        eventProbe.ExpectMsg<ScatterGatherFinished>(evt =>
        {
            evt.BatchId.Should().Be("batch-evt");
            evt.ResultCount.Should().Be(1);
            return true;
        });
    }

    [Fact]
    public void ScatterGather_EmptyBatch_ReturnsImmediately()
    {
        var coordinator = Sys.ActorOf(ScatterGatherCoordinatorActor.Props(), "scatter-gather-empty");
        var replyProbe = CreateTestProbe();

        coordinator.Tell(new ScatterGatherCommand("empty", []), replyProbe.Ref);

        replyProbe.ExpectMsg<ScatterGatherCompleted>(msg =>
        {
            msg.BatchId.Should().Be("empty");
            msg.Results.Should().BeEmpty();
            return true;
        });
    }
}
