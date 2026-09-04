using Akka.Actor;
using Akka.Hosting;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using AkkaTeach.Core.Clients;
using Microsoft.Extensions.Options;

namespace AkkaTeach.Worker;

/// <summary>
/// Background worker that periodically triggers paginated API collection and worker-pool processing.
/// </summary>
public sealed class TeachingBackgroundWorker : BackgroundService
{
    private static readonly TimeSpan CycleInterval = TimeSpan.FromSeconds(30);

    private readonly IActorRegistry _actorRegistry;
    private readonly IOptions<DataIngestionOptions> _options;
    private readonly ILogger<TeachingBackgroundWorker> _logger;

    public TeachingBackgroundWorker(
        IActorRegistry actorRegistry,
        IOptions<DataIngestionOptions> options,
        ILogger<TeachingBackgroundWorker> logger)
    {
        _actorRegistry = actorRegistry;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IActorRef ingestion = await _actorRegistry.GetAsync<DataIngestionActor>(stoppingToken);

        int expectedRecords = _options.Value.TotalPages * _options.Value.PageSize;
        _logger.LogInformation(
            "Data ingestion worker ready — mock API: {Pages} pages × {PageSize} records = {Total} items, pool size {Pool}",
            _options.Value.TotalPages,
            _options.Value.PageSize,
            expectedRecords,
            _options.Value.WorkerPoolSize);

        int cycle = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            cycle++;
            _logger.LogInformation("Starting ingestion cycle {Cycle}", cycle);
            ingestion.Tell(new CollectDataCommand($"cycle-{cycle}"));

            await Task.Delay(CycleInterval, stoppingToken);
        }
    }
}
