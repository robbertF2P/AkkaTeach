using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Akka.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;

namespace AkkaTeach.Tests.Phase2_IdentityAndLifecycle;

/// <summary>
/// PHASE 2b — Actor lifecycle: start, stop, restart, supervision, and death watch.
/// </summary>
/// <remarks>
/// <para><b>The distinction that matters:</b></para>
/// <list type="bullet">
/// <item><description><b>Restart</b> — the actor <em>instance</em> is thrown away and rebuilt after a
/// failure. Same <see cref="IActorRef"/>, same mailbox, <b>state is lost</b>. Callers notice nothing.
/// This is the <b>default</b> behaviour.</description></item>
/// <item><description><b>Stop</b> — permanent. The <see cref="IActorRef"/> becomes a dead letter box.</description></item>
/// </list>
/// <para><b>Who decides?</b> The <em>parent</em>. Its <c>SupervisorStrategy</c> turns a child's
/// exception into Restart, Stop, Resume, or Escalate. Override it to change the outcome —
/// see <see cref="StoppingSupervisorActor"/>.</para>
/// <para><b>Hook order on restart:</b> <c>PreStart</c> → (failure) → <c>PreRestart</c> →
/// <c>PostStop</c> → <c>PostRestart</c> → <c>PreStart</c> on the new instance.
/// The <c>PostStop</c> in the middle surprises people: the default <c>PreRestart</c>
/// calls <c>PostStop</c> so your cleanup runs before the instance is replaced, and the default
/// <c>PostRestart</c> calls <c>PreStart</c> so your setup runs again.</para>
/// </remarks>
public sealed class Lesson3_ActorLifecycleTests(ITestOutputHelper output) : TeachingTestKit(output)
{

    /// <summary>
    /// PreStart runs before the actor handles its first message — the place for setup.
    /// </summary>
    [Fact]
    public void PreStart_RunsBeforeTheFirstMessage()
    {
        TestProbe watcher = CreateTestProbe();

        Sys.ActorOf(LifecycleReportingActor.Props(watcher.Ref), "lifecycle");

        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreStart");
    }

    /// <summary>
    /// PostStop runs after the actor is stopped — the place for cleanup.
    /// </summary>
    [Fact]
    public void PostStop_RunsWhenTheActorIsStopped()
    {
        TestProbe watcher = CreateTestProbe();
        IActorRef actor = Sys.ActorOf(LifecycleReportingActor.Props(watcher.Ref), "lifecycle");
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreStart");

        Sys.Stop(actor);

        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PostStop");
    }

    /// <summary>
    /// The default reaction to a failure is RESTART, not stop:
    /// PreRestart on the old instance, then PostRestart on the new one.
    /// Note the PostStop and PreStart in between — see ExpectRestartSequence.
    /// An exception in an actor does not take the actor down.
    /// </summary>
    [Fact]
    public void WhenAnActorThrows_ItIsRestartedByDefault()
    {
        TestProbe watcher = CreateTestProbe();
        IActorRef actor = Sys.ActorOf(LifecycleReportingActor.Props(watcher.Ref), "lifecycle");
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreStart");

        // EventFilter suppresses the expected error log so it does not pollute the run.
        EventFilter.Exception<InvalidOperationException>().ExpectOne(() => actor.Tell(new BoomCommand()));

        ExpectRestartSequence(watcher);
    }

    /// <summary>
    /// A restart keeps the same IActorRef — the address survives the failure,
    /// so everyone holding a reference keeps working.
    /// </summary>
    [Fact]
    public void AfterARestart_TheSameIActorRefStillWorks()
    {
        TestProbe watcher = CreateTestProbe();
        TestProbe probe = CreateTestProbe();
        IActorRef actor = Sys.ActorOf(LifecycleReportingActor.Props(watcher.Ref), "lifecycle");
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreStart");

        EventFilter.Exception<InvalidOperationException>().ExpectOne(() => actor.Tell(new BoomCommand()));
        ExpectRestartSequence(watcher);

        actor.Tell(new CountMessageCommand(), probe.Ref);

        probe.ExpectMsg<HandledCountResponse>();
    }

    /// <summary>
    /// A restart builds a brand new instance, so in-memory state is lost.
    /// This is why durable state must live outside the actor (or use persistence).
    /// </summary>
    [Fact]
    public void ARestart_ResetsInMemoryState()
    {
        TestProbe watcher = CreateTestProbe();
        TestProbe probe = CreateTestProbe();
        IActorRef actor = Sys.ActorOf(LifecycleReportingActor.Props(watcher.Ref), "lifecycle");
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreStart");

        actor.Tell(new CountMessageCommand(), probe.Ref);
        probe.ExpectMsg<HandledCountResponse>(msg => msg.Count == 1);

        actor.Tell(new CountMessageCommand(), probe.Ref);
        probe.ExpectMsg<HandledCountResponse>(msg => msg.Count == 2);

        EventFilter.Exception<InvalidOperationException>().ExpectOne(() => actor.Tell(new BoomCommand()));
        ExpectRestartSequence(watcher);

        // Counter is back to 1, not 3 — the old instance and its state are gone.
        actor.Tell(new CountMessageCommand(), probe.Ref);
        probe.ExpectMsg<HandledCountResponse>(msg => msg.Count == 1);
    }

    /// <summary>
    /// The parent decides. Override SupervisorStrategy with Directive.Stop and the same
    /// failure kills the child instead of restarting it.
    /// </summary>
    [Fact]
    public async Task AParentCanChooseToStopAFailingChildInstead()
    {
        TestProbe watcher = CreateTestProbe();
        IActorRef supervisor = Sys.ActorOf(StoppingSupervisorActor.Props(watcher.Ref), "supervisor");
        ChildSpawnedResponse spawned =
            await supervisor.Ask<ChildSpawnedResponse>(new SpawnChildCommand("child"), TimeSpan.FromSeconds(3));
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreStart");

        EventFilter.Exception<InvalidOperationException>().ExpectOne(() => spawned.Child.Tell(new BoomCommand()));

        // Straight to PostStop — no PreRestart, no PostRestart.
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PostStop");
    }

    /// <summary>
    /// Watch lets one actor be notified when another dies (death watch).
    /// This is how you react to termination instead of polling.
    /// </summary>
    [Fact]
    public void Watch_NotifiesYouWhenAnActorTerminates()
    {
        TestProbe watcher = CreateTestProbe();
        IActorRef actor = Sys.ActorOf(LifecycleReportingActor.Props(watcher.Ref), "lifecycle");
        TestProbe deathWatcher = CreateTestProbe();

        deathWatcher.Watch(actor);
        Sys.Stop(actor);

        deathWatcher.ExpectTerminated(actor);
    }

    /// <summary>
    /// A restart is NOT a termination — watchers are not notified, because from the
    /// outside nothing died. Only a real stop produces Terminated.
    /// </summary>
    [Fact]
    public void ARestart_DoesNotNotifyWatchers()
    {
        TestProbe watcher = CreateTestProbe();
        IActorRef actor = Sys.ActorOf(LifecycleReportingActor.Props(watcher.Ref), "lifecycle");
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreStart");

        TestProbe deathWatcher = CreateTestProbe();
        deathWatcher.Watch(actor);

        EventFilter.Exception<InvalidOperationException>().ExpectOne(() => actor.Tell(new BoomCommand()));
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreRestart");

        deathWatcher.ExpectNoMsg(TimeSpan.FromMilliseconds(300));
    }

    /// <summary>
    /// Asserts the full default restart sequence, including the PostStop/PreStart that the
    /// default PreRestart/PostRestart implementations trigger.
    /// </summary>
    private static void ExpectRestartSequence(TestProbe watcher)
    {
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreRestart");
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PostStop");
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PostRestart");
        watcher.ExpectMsg<LifecycleSignal>(signal => signal.Hook == "PreStart");
    }

    /// <summary>
    /// Stopping a parent stops its children too — the tree is torn down together.
    /// This is what makes supervision hierarchies safe to reason about.
    /// </summary>
    [Fact]
    public async Task StoppingAParent_AlsoStopsItsChildren()
    {
        TestProbe watcher = CreateTestProbe();
        IActorRef parent = Sys.ActorOf(ChildSpawningParentActor.Props(watcher.Ref), "parent");
        ChildSpawnedResponse spawned =
            await parent.Ask<ChildSpawnedResponse>(new SpawnChildCommand("child"), TimeSpan.FromSeconds(3));

        TestProbe deathWatcher = CreateTestProbe();
        deathWatcher.Watch(spawned.Child);

        Sys.Stop(parent);

        deathWatcher.ExpectTerminated(spawned.Child);
    }
}
