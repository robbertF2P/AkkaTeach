using Akka.Actor;
using Akka.Event;
using Akka.Routing;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Clients;
using Microsoft.Extensions.Options;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Orchestrates paginated API collection and fans records out to a router-backed worker pool.
/// Uses <c>Become</c> to track fetch/process cycles without blocking the mailbox.
/// </summary>
public sealed class DataIngestionActor : ReceiveActor
{
    private readonly IDataApiClient _apiClient;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IActorRef _workerPool;
    private readonly int _workerPoolSize;

    private string _collectionId = string.Empty;
    private IActorRef? _requester;
    private int _totalPages;
    private int _currentPage;
    private int _pagesCollected;
    private int _recordsProcessed;
    private int _recordsPendingOnPage;
    private int _totalRecordsExpected;
    private long _startedAtTicks;

    public DataIngestionActor(IDataApiClient apiClient, IOptions<DataIngestionOptions> options)
    {
        _apiClient = apiClient;
        _workerPoolSize = options.Value.WorkerPoolSize;
        _workerPool = Context.ActorOf(
            DataRecordWorkerActor.Props().WithRouter(new RoundRobinPool(_workerPoolSize)),
            "data-worker-pool");

        Become(Idle);
    }

    private void Idle()
    {
        Receive<CollectDataCommand>(StartCollection);
        Receive<GetIngestionStatusQuery>(_ => Sender.Tell(CreateStatus("Idle")));
    }

    private void StartCollection(CollectDataCommand command)
    {
        _collectionId = command.CollectionId ?? Guid.NewGuid().ToString("N")[..8];
        _requester = Sender.IsNobody() ? null : Sender;
        _currentPage = 0;
        _pagesCollected = 0;
        _recordsProcessed = 0;
        _recordsPendingOnPage = 0;
        _totalRecordsExpected = 0;
        _startedAtTicks = DateTime.UtcNow.Ticks;

        _log.Info("Starting data collection {CollectionId} with {PoolSize} workers", _collectionId, _workerPoolSize);
        FetchNextPage();
        Become(Collecting);
    }

    private void Collecting()
    {
        Receive<ApiPageReceived>(OnPageReceived);
        Receive<ApiPageFetchFailed>(OnPageFetchFailed);
        Receive<DataRecordProcessed>(OnRecordProcessed);
        Receive<GetIngestionStatusQuery>(_ => Sender.Tell(CreateStatus("Collecting")));
    }

    private void FetchNextPage()
    {
        int nextPage = _currentPage + 1;
        _log.Debug("Fetching API page {Page}", nextPage);

        _apiClient.FetchPageAsync(nextPage).PipeTo(
            Self,
            Self,
            success: page => new ApiPageReceived(page),
            failure: ex => new ApiPageFetchFailed(ex));
    }

    private int _recordsOnCurrentPage;

    private void OnPageReceived(ApiPageReceived message)
    {
        ApiDataPage page = message.Page;
        _currentPage = page.PageNumber;
        _totalPages = page.TotalPages;
        _totalRecordsExpected = page.TotalPages * page.Records.Count;

        if (_pagesCollected == 0)
        {
            Context.System.EventStream.Publish(
                new DataCollectionStarted(_collectionId, page.TotalPages, _totalRecordsExpected));
        }

        _recordsOnCurrentPage = page.Records.Count;
        _recordsPendingOnPage = page.Records.Count;
        _log.Info(
            "Page {Page}/{TotalPages} received with {Count} records",
            page.PageNumber,
            page.TotalPages,
            page.Records.Count);

        if (page.Records.Count == 0)
        {
            CompletePage();
            return;
        }

        foreach (ExternalDataRecord record in page.Records)
        {
            _workerPool.Tell(new ProcessDataRecordCommand(record), Self);
        }
    }

    private void OnRecordProcessed(DataRecordProcessed result)
    {
        _recordsProcessed++;
        _recordsPendingOnPage--;

        if (_recordsPendingOnPage <= 0)
        {
            CompletePage();
        }
    }

    private void CompletePage()
    {
        _pagesCollected++;
        Context.System.EventStream.Publish(new DataPageProcessed(_currentPage, _recordsOnCurrentPage));

        if (_currentPage >= _totalPages)
        {
            FinishCollection();
            return;
        }

        FetchNextPage();
    }

    private void FinishCollection()
    {
        long elapsedMs = (DateTime.UtcNow.Ticks - _startedAtTicks) / TimeSpan.TicksPerMillisecond;
        _log.Info(
            "Collection {CollectionId} completed: {Records} records in {ElapsedMs}ms",
            _collectionId,
            _recordsProcessed,
            elapsedMs);

        Context.System.EventStream.Publish(
            new DataCollectionCompleted(_collectionId, _recordsProcessed, elapsedMs));

        IngestionStatusResponse response = CreateStatus("Completed");
        _requester?.Tell(response);
        _requester = null;
        Become(Idle);
    }

    private void OnPageFetchFailed(ApiPageFetchFailed failure)
    {
        _log.Error(failure.Exception, "API page fetch failed during collection {CollectionId}", _collectionId);
        _requester?.Tell(CreateStatus("Failed"));
        _requester = null;
        Become(Idle);
    }

    private IngestionStatusResponse CreateStatus(string state) =>
        new(state, _pagesCollected, _recordsProcessed, _totalRecordsExpected);

    private sealed record ApiPageReceived(ApiDataPage Page);

    private sealed record ApiPageFetchFailed(Exception Exception);

    public static Props Props(IDataApiClient apiClient, int workerPoolSize) =>
        Akka.Actor.Props.Create(() => new DataIngestionActor(
            apiClient,
            Microsoft.Extensions.Options.Options.Create(new DataIngestionOptions { WorkerPoolSize = workerPoolSize })));
}
