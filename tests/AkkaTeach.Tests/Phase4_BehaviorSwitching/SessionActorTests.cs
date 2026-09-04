using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Phase4_BehaviorSwitching;

public sealed class SessionActorTests(ITestOutputHelper output) : TeachingTestKit(output)
{

    [Fact]
    public void NewActor_StartsInIdleState()
    {
        var session = Sys.ActorOf(SessionActor.Props(), "session");
        var probe = CreateTestProbe();

        session.Tell(new GetSessionStateQuery(), probe.Ref);

        probe.ExpectMsg<SessionStateResponse>(msg =>
        {
            msg.State.Should().Be("Idle");
            msg.SessionId.Should().BeNull();
            msg.StepsRecorded.Should().Be(0);
            return true;
        });
    }

    [Fact]
    public void StartSession_BecomesActive_AndAcceptsProgress()
    {
        var session = Sys.ActorOf(SessionActor.Props(), "session");
        var probe = CreateTestProbe();

        session.Tell(new StartSessionCommand("lesson-1"), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>(msg => msg.State == "Active" && msg.SessionId == "lesson-1");

        session.Tell(new RecordProgressCommand(3), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>(msg =>
        {
            msg.State.Should().Be("Active");
            msg.StepsRecorded.Should().Be(3);
            return true;
        });
    }

    [Fact]
    public void EndSession_BecomesCompleted_AndRejectsFurtherCommands()
    {
        var session = Sys.ActorOf(SessionActor.Props(), "session");
        var probe = CreateTestProbe();

        session.Tell(new StartSessionCommand("lesson-2"), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>();

        session.Tell(new RecordProgressCommand(2), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>();

        session.Tell(new EndSessionCommand(), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>(msg =>
        {
            msg.State.Should().Be("Completed");
            msg.StepsRecorded.Should().Be(2);
            return true;
        });

        session.Tell(new RecordProgressCommand(99), probe.Ref);
        probe.ExpectNoMsg(TimeSpan.FromMilliseconds(300));

        session.Tell(new GetSessionStateQuery(), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>(msg => msg.State == "Completed");
    }

    [Fact]
    public void ResetSession_FromCompleted_ReturnsToIdle()
    {
        var session = Sys.ActorOf(SessionActor.Props(), "session");
        var probe = CreateTestProbe();

        session.Tell(new StartSessionCommand("lesson-3"), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>();

        session.Tell(new EndSessionCommand(), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>(msg => msg.State == "Completed");

        session.Tell(new ResetSessionCommand(), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>(msg => msg.State == "Idle");

        session.Tell(new StartSessionCommand("lesson-4"), probe.Ref);
        probe.ExpectMsg<SessionStateResponse>(msg => msg.State == "Active" && msg.SessionId == "lesson-4");
    }

    [Fact]
    public void SessionLifecycle_PublishesEventsOnEventStream()
    {
        var session = Sys.ActorOf(SessionActor.Props(), "session");
        var eventProbe = CreateTestProbe();
        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(SessionStarted));
        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(SessionEnded));

        session.Tell(new StartSessionCommand("evt-session"), TestActor);
        ExpectMsg<SessionStateResponse>();

        eventProbe.ExpectMsg<SessionStarted>(evt => evt.SessionId == "evt-session");

        session.Tell(new RecordProgressCommand(1), TestActor);
        ExpectMsg<SessionStateResponse>();

        session.Tell(new EndSessionCommand(), TestActor);
        ExpectMsg<SessionStateResponse>();

        eventProbe.ExpectMsg<SessionEnded>(evt =>
        {
            evt.SessionId.Should().Be("evt-session");
            evt.TotalSteps.Should().Be(1);
            return true;
        });
    }
}
