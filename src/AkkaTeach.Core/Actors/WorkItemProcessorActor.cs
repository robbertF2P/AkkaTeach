using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Child worker that processes a single work item and replies to the original sender.
/// Demonstrates simple request/reply actor communication.
/// </summary>
public sealed class WorkItemProcessorActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public WorkItemProcessorActor()
    {
        Receive<ProcessWorkItemCommand>(command =>
        {
            int result = command.Payload * 2;
            _log.Info("Processed item {ItemId}: {Payload} -> {Result}", command.ItemId, command.Payload, result);
            Sender.Tell(new WorkItemProcessed(command.ItemId, result));
        });
    }

    public static Props Props() => Akka.Actor.Props.Create<WorkItemProcessorActor>();
}
