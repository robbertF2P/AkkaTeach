using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Spawns kamikaze workers, watches them, and forwards results to the original caller.
/// </summary>
/// <remarks>
/// The manager survives worker failures. If a worker stops without sending
/// <see cref="KamikazeWorkCompleted"/>, the manager treats the task as failed via
/// <see cref="Terminated"/>.
/// </remarks>
public sealed class KamikazeManagerActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private IActorRef? _pendingRequester;
    private string? _pendingTaskId;
    private bool _resultReceived;

    public KamikazeManagerActor()
    {
        Receive<RunKamikazeTaskCommand>(command => StartTask(command));
        Receive<KamikazeWorkCompleted>(completed => CompleteTask(completed));
        Receive<Terminated>(terminated => HandleWorkerTerminated(terminated));
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 0,
            withinTimeRange: TimeSpan.FromSeconds(1),
            localOnlyDecider: _ => Directive.Stop);

    private void StartTask(RunKamikazeTaskCommand command)
    {
        _pendingRequester = Sender.IsNobody() ? null : Sender;
        _pendingTaskId = command.TaskId;
        _resultReceived = false;

        var worker = Context.ActorOf(
            KamikazeWorkerActor.Props(command.TaskId, command.ShouldFail),
            $"kamikaze-{command.TaskId}");

        Context.Watch(worker);
        _log.Info("Spawned kamikaze worker for task {TaskId}", command.TaskId);
    }

    private void CompleteTask(KamikazeWorkCompleted completed)
    {
        _resultReceived = true;

        var success = new KamikazeTaskSucceeded(completed.TaskId, completed.Result);
        _pendingRequester?.Tell(success);
        Context.System.EventStream.Publish(
            new KamikazeTaskFinished(completed.TaskId, Succeeded: true, completed.Result));

        _log.Info("Kamikaze task {TaskId} succeeded", completed.TaskId);
        ClearPending();
    }

    private void HandleWorkerTerminated(Terminated terminated)
    {
        if (_resultReceived || _pendingTaskId is null)
        {
            return;
        }

        var reason = $"Worker {terminated.ActorRef.Path} terminated before reporting success";
        var failure = new KamikazeTaskFailed(_pendingTaskId, reason);
        _pendingRequester?.Tell(failure);
        Context.System.EventStream.Publish(
            new KamikazeTaskFinished(_pendingTaskId, Succeeded: false, reason));

        _log.Warning("Kamikaze task {TaskId} failed: {Reason}", _pendingTaskId, reason);
        ClearPending();
    }

    private void ClearPending()
    {
        _pendingRequester = null;
        _pendingTaskId = null;
        _resultReceived = false;
    }

    public static Props Props() => Akka.Actor.Props.Create<KamikazeManagerActor>();
}
