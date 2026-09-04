using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Routes work to a child processor and collects results.
/// Uses <c>Tell</c> with <c>Self</c> as sender so the coordinator receives the child reply,
/// then <c>Become</c> to handle the reply without blocking and forwards the result to the caller.
/// </summary>
public sealed class WorkCoordinatorActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IActorRef _processor;
    private int _completedCount;
    private IActorRef? _pendingSender;

    public WorkCoordinatorActor()
    {
        _processor = Context.ActorOf(WorkItemProcessorActor.Props(), "processor");
        Become(Active);
    }

    private void Active()
    {
        Receive<ProcessWorkItemCommand>(command =>
        {
            _pendingSender = Sender;
            _log.Debug("Sending work item {ItemId} to processor", command.ItemId);
            _processor.Tell(command, Self);
            Become(WaitingForResult);
        });

        Receive<GetCompletedCountQuery>(_ => Sender.Tell(new CompletedCountResponse(_completedCount)));
    }

    private void WaitingForResult()
    {
        Receive<WorkItemProcessed>(result =>
        {
            _completedCount++;
            _log.Info("Coordinator recorded completion #{Count} for {ItemId}", _completedCount, result.ItemId);
            Context.System.EventStream.Publish(new WorkItemCompleted(result.ItemId, result.Result));
            if (_pendingSender is not null && !_pendingSender.IsNobody())
            {
                _pendingSender.Tell(result);
            }

            _pendingSender = null;
            Become(Active);
        });

        Receive<GetCompletedCountQuery>(_ => Sender.Tell(new CompletedCountResponse(_completedCount)));
    }

    protected override void PreStart()
    {
        base.PreStart();
        _log.Info("Work coordinator started with child processor");
        Context.System.EventStream.Publish(new ActorSystemStarted(Self.Path.Name));
    }

    public static Props Props() => Akka.Actor.Props.Create<WorkCoordinatorActor>();
}
