using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Pool worker that transforms a single API record (simulate enrichment / normalization).
/// </summary>
public sealed class DataRecordWorkerActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public DataRecordWorkerActor()
    {
        Receive<ProcessDataRecordCommand>(command =>
        {
            int processed = command.Record.Value * 2;
            _log.Debug(
                "Worker {Worker} processed {RecordId} from {Source}: {Value} -> {Processed}",
                Self.Path.Name,
                command.Record.Id,
                command.Record.Source,
                command.Record.Value,
                processed);

            Sender.Tell(new DataRecordProcessed(command.Record.Id, processed));
        });
    }

    public static Props Props() => Akka.Actor.Props.Create<DataRecordWorkerActor>();
}
