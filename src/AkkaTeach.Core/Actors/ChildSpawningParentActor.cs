using Akka.Actor;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// A plain parent that spawns children on demand and uses the <b>default</b> supervision strategy
/// (restart the child on failure).
/// </summary>
/// <remarks>Send <see cref="SpawnChildCommand"/>; the reply is a <see cref="ChildSpawnedResponse"/>.</remarks>
public sealed class ChildSpawningParentActor : ReceiveActor
{
    private readonly IActorRef _watcher;

    public ChildSpawningParentActor(IActorRef watcher)
    {
        _watcher = watcher;

        Receive<SpawnChildCommand>(command =>
        {
            IActorRef child = Context.ActorOf(LifecycleReportingActor.Props(_watcher), command.Name);
            Sender.Tell(new ChildSpawnedResponse(child));
        });
    }

    public static Props Props(IActorRef watcher) =>
        Akka.Actor.Props.Create(() => new ChildSpawningParentActor(watcher));
}
