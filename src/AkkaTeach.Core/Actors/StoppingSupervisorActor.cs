using Akka.Actor;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// A parent that supervises one child with an explicit <b>Stop</b> strategy.
/// </summary>
/// <remarks>
/// <para>The default supervision strategy is <see cref="Directive.Restart"/>. This actor exists to
/// show the contrast: a parent can decide that a failing child should be <b>stopped</b> instead.</para>
/// <para>Send <see cref="SpawnChildCommand"/> to create the child; the reply is the child's
/// <see cref="IActorRef"/>.</para>
/// </remarks>
public sealed class StoppingSupervisorActor : ReceiveActor
{
    private readonly IActorRef _watcher;

    public StoppingSupervisorActor(IActorRef watcher)
    {
        _watcher = watcher;

        Receive<SpawnChildCommand>(command =>
        {
            IActorRef child = Context.ActorOf(LifecycleReportingActor.Props(_watcher), command.Name);
            Sender.Tell(new ChildSpawnedResponse(child));
        });
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 10,
            withinTimeRange: TimeSpan.FromMinutes(1),
            localOnlyDecider: _ => Directive.Stop);

    public static Props Props(IActorRef watcher) =>
        Akka.Actor.Props.Create(() => new StoppingSupervisorActor(watcher));
}
