using Akka.Actor;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Phase3_Messaging;

public sealed class WorkCoordinatorActorTests(ITestOutputHelper output) : TeachingTestKit(output)
{
    [Fact]
    public void ProcessWorkItem_ForwardsToProcessor_AndRepliesWithDoubledResult()
    {
        var coordinator = Sys.ActorOf(WorkCoordinatorActor.Props(), "coordinator");
        var probe = CreateTestProbe();

        coordinator.Tell(new ProcessWorkItemCommand("alpha", 21), probe.Ref);

        probe.ExpectMsg<WorkItemProcessed>(msg =>
        {
            msg.ItemId.Should().Be("alpha");
            msg.Result.Should().Be(42);
            return true;
        });
    }

    [Fact]
    public void ProcessWorkItem_IncrementsCompletedCount()
    {
        var coordinator = Sys.ActorOf(WorkCoordinatorActor.Props(), "coordinator");
        var probe = CreateTestProbe();

        coordinator.Tell(new ProcessWorkItemCommand("one", 1), probe.Ref);
        probe.ExpectMsg<WorkItemProcessed>();

        coordinator.Tell(new ProcessWorkItemCommand("two", 2), probe.Ref);
        probe.ExpectMsg<WorkItemProcessed>();

        coordinator.Tell(new GetCompletedCountQuery(), probe.Ref);
        probe.ExpectMsg<CompletedCountResponse>(msg => msg.Count == 2);
    }

    [Fact]
    public void WorkItemCompleted_IsPublishedOnEventStream()
    {
        var coordinator = Sys.ActorOf(WorkCoordinatorActor.Props(), "coordinator");
        var eventProbe = CreateTestProbe();
        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(WorkItemCompleted));

        var replyProbe = CreateTestProbe();
        coordinator.Tell(new ProcessWorkItemCommand("evt-1", 5), replyProbe.Ref);
        replyProbe.ExpectMsg<WorkItemProcessed>();

        eventProbe.ExpectMsg<WorkItemCompleted>(evt =>
        {
            evt.ItemId.Should().Be("evt-1");
            evt.Result.Should().Be(10);
            return true;
        });
    }
}
