using Akka.Actor;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Teaching actor that reports every lifecycle hook to a watcher so the actor
/// lifecycle can be observed in tests.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle hooks, in order:</b></para>
/// <list type="number">
/// <item><description><c>PreStart</c> — after the instance is constructed, before any message.</description></item>
/// <item><description><c>PreRestart</c> — on failure, before the instance is replaced.</description></item>
/// <item><description><c>PostRestart</c> — on the new instance (calls <c>PreStart</c> by default).</description></item>
/// <item><description><c>PostStop</c> — after the actor is stopped for good.</description></item>
/// </list>
/// <para><b>Key point:</b> a restart replaces the actor <em>instance</em> but keeps the same
/// <see cref="IActorRef"/> and mailbox. Callers never notice. A stop is permanent.</para>
/// </remarks>
public sealed class LifecycleReportingActor : ReceiveActor
{
    private readonly IActorRef _watcher;
    private int _messagesHandled;

    public LifecycleReportingActor(IActorRef watcher)
    {
        _watcher = watcher;

        Receive<BoomCommand>(_ => throw new InvalidOperationException("Deliberate failure"));

        Receive<CountMessageCommand>(_ =>
        {
            _messagesHandled++;
            Sender.Tell(new HandledCountResponse(_messagesHandled));
        });
    }

    protected override void PreStart() => _watcher.Tell(new LifecycleSignal("PreStart", Self.Path.Name));

    protected override void PostStop() => _watcher.Tell(new LifecycleSignal("PostStop", Self.Path.Name));

    protected override void PreRestart(Exception reason, object? message)
    {
        _watcher.Tell(new LifecycleSignal("PreRestart", Self.Path.Name));
        base.PreRestart(reason, message);
    }

    protected override void PostRestart(Exception reason)
    {
        _watcher.Tell(new LifecycleSignal("PostRestart", Self.Path.Name));
        base.PostRestart(reason);
    }

    public static Props Props(IActorRef watcher) =>
        Akka.Actor.Props.Create(() => new LifecycleReportingActor(watcher));
}
