using Akka.Actor;
using Akka.Event;
using Akka.Routing;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Fans work out to a worker pool, counts replies, and aggregates when all are received.
/// </summary>
/// <remarks>
/// <para><b>Pattern:</b> scatter-gather — you know how many responses to expect, so you
/// <c>Tell</c> N workers with <c>Self</c> as sender, <c>Become(Aggregating)</c>, and reply
/// once <c>_receivedCount == _expectedCount</c>.</para>
/// <para>See guide §16 and <see cref="WorkCoordinatorActor"/> (single-item variant).</para>
/// </remarks>
public sealed class ScatterGatherCoordinatorActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IActorRef _workerPool;

    private IActorRef? _requester;
    private string _batchId = string.Empty;
    private int _expectedCount;
    private int _receivedCount;
    private readonly List<WorkItemProcessed> _results = [];

    public ScatterGatherCoordinatorActor(int workerPoolSize = 3)
    {
        _workerPool = Context.ActorOf(
            WorkItemProcessorActor.Props().WithRouter(new RoundRobinPool(workerPoolSize)),
            "scatter-workers");

        Become(Idle);
    }

    private void Idle()
    {
        Receive<ScatterGatherCommand>(StartGather);

        Receive<ScatterGatherStatusQuery>(_ =>
            Sender.Tell(new ScatterGatherStatusResponse("Idle", 0, 0)));
    }

    private void StartGather(ScatterGatherCommand command)
    {
        if (command.Items.Count == 0)
        {
            Sender.Tell(new ScatterGatherCompleted(command.BatchId, []));
            return;
        }

        _requester = Sender.IsNobody() ? null : Sender;
        _batchId = command.BatchId;
        _expectedCount = command.Items.Count;
        _receivedCount = 0;
        _results.Clear();

        _log.Info(
            "Scatter-gather batch {BatchId} fanning out {Count} items to worker pool",
            _batchId,
            _expectedCount);

        foreach (var item in command.Items)
        {
            _workerPool.Tell(item, Self);
        }

        Become(Aggregating);
    }

    private void Aggregating()
    {
        Receive<WorkItemProcessed>(result =>
        {
            _results.Add(result);
            _receivedCount++;

            _log.Debug(
                "Scatter-gather batch {BatchId} received {Received}/{Expected} from {ItemId}",
                _batchId,
                _receivedCount,
                _expectedCount,
                result.ItemId);

            if (_receivedCount < _expectedCount)
            {
                return;
            }

            var completed = new ScatterGatherCompleted(_batchId, _results.ToList());
            _requester?.Tell(completed);
            Context.System.EventStream.Publish(new ScatterGatherFinished(_batchId, _results.Count));
            _log.Info("Scatter-gather batch {BatchId} completed with {Count} results", _batchId, _results.Count);
            ClearBatch();
            Become(Idle);
        });

        Receive<ScatterGatherStatusQuery>(_ =>
            Sender.Tell(new ScatterGatherStatusResponse("Aggregating", _receivedCount, _expectedCount)));

        Receive<ScatterGatherCommand>(_ =>
            _log.Warning("Ignoring new batch while aggregating {BatchId}", _batchId));
    }

    private void ClearBatch()
    {
        _requester = null;
        _batchId = string.Empty;
        _expectedCount = 0;
        _receivedCount = 0;
        _results.Clear();
    }

    public static Props Props(int workerPoolSize = 3) =>
        Akka.Actor.Props.Create(() => new ScatterGatherCoordinatorActor(workerPoolSize));
}
