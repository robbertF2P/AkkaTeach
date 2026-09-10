using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Actors;

/// <summary>
/// Tests for the kamikaze worker pattern: short-lived child, death watch, parent survives.
/// </summary>
public sealed class KamikazeActorTests : TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
    }

    [Fact]
    public void RunTask_WhenWorkerSucceeds_ReportsSuccessAndSelfTerminates()
    {
        var manager = Sys.ActorOf(KamikazeManagerActor.Props(), "kamikaze-manager");
        var replyProbe = CreateTestProbe();
        var eventProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(KamikazeTaskFinished));

        manager.Tell(new RunKamikazeTaskCommand("task-1"), replyProbe.Ref);

        replyProbe.ExpectMsg<KamikazeTaskSucceeded>(msg =>
        {
            msg.TaskId.Should().Be("task-1");
            msg.Result.Should().Contain("task-1");
            return true;
        });

        eventProbe.ExpectMsg<KamikazeTaskFinished>(msg =>
        {
            msg.TaskId.Should().Be("task-1");
            msg.Succeeded.Should().BeTrue();
            return true;
        });
    }

    [Fact]
    public void RunTask_WhenWorkerCrashes_ReportsFailureViaDeathWatch()
    {
        var manager = Sys.ActorOf(KamikazeManagerActor.Props(), "kamikaze-manager-fail");
        var replyProbe = CreateTestProbe();
        var eventProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(KamikazeTaskFinished));

        manager.Tell(new RunKamikazeTaskCommand("task-boom", ShouldFail: true), replyProbe.Ref);

        replyProbe.ExpectMsg<KamikazeTaskFailed>(msg =>
        {
            msg.TaskId.Should().Be("task-boom");
            msg.Reason.Should().Contain("terminated");
            return true;
        });

        eventProbe.ExpectMsg<KamikazeTaskFinished>(msg =>
        {
            msg.TaskId.Should().Be("task-boom");
            msg.Succeeded.Should().BeFalse();
            return true;
        });
    }

    [Fact]
    public void RunTask_ManagerRemainsAlive_AfterWorkerSelfTerminates()
    {
        var manager = Sys.ActorOf(KamikazeManagerActor.Props(), "kamikaze-manager-alive");
        var replyProbe = CreateTestProbe();

        manager.Tell(new RunKamikazeTaskCommand("first"), replyProbe.Ref);
        replyProbe.ExpectMsg<KamikazeTaskSucceeded>();

        manager.Tell(new RunKamikazeTaskCommand("second"), replyProbe.Ref);
        replyProbe.ExpectMsg<KamikazeTaskSucceeded>(msg => msg.TaskId == "second");
    }
}
