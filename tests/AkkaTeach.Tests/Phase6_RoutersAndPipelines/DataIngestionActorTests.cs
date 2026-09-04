using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using AkkaTeach.Core.Clients;
using AkkaTeach.Worker.Clients;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AkkaTeach.Tests.Phase6_RoutersAndPipelines;

public sealed class DataIngestionActorTests(ITestOutputHelper output) : TeachingTestKit(output)
{
    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<IDataApiClient, FakeDataApiClient>();
        services.Configure<DataIngestionOptions>(options =>
        {
            options.WorkerPoolSize = 2;
            options.PageSize = 3;
            options.TotalPages = 2;
        });
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        base.ConfigureAkka(builder, provider);

        builder.WithActors((system, registry, resolver) =>
        {
            var ingestion = system.ActorOf(resolver.Props<DataIngestionActor>(), "data-ingestion");
            registry.Register<DataIngestionActor>(ingestion);
        });
    }

    [Fact]
    public async Task CollectData_FetchesAllPages_AndProcessesEveryRecord()
    {
        var ingestion = await ActorRegistry.GetAsync<DataIngestionActor>();
        var probe = CreateTestProbe();

        ingestion.Tell(new CollectDataCommand("test-run"), probe.Ref);

        probe.ExpectMsg<IngestionStatusResponse>(msg =>
        {
            msg.State.Should().Be("Completed");
            msg.PagesCollected.Should().Be(2);
            msg.RecordsProcessed.Should().Be(6);
            msg.TotalRecords.Should().Be(6);
            return true;
        });
    }

    [Fact]
    public void CollectData_PublishesLifecycleEvents()
    {
        var ingestion = Sys.ActorOf(
            DataIngestionActor.Props(new FakeDataApiClient(pageSize: 2, totalPages: 2), workerPoolSize: 2),
            "ingestion");

        var eventProbe = CreateTestProbe();
        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(DataCollectionStarted));
        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(DataCollectionCompleted));

        var probe = CreateTestProbe();
        ingestion.Tell(new CollectDataCommand("evt-run"), probe.Ref);

        eventProbe.ExpectMsg<DataCollectionStarted>(evt =>
        {
            evt.CollectionId.Should().Be("evt-run");
            evt.TotalPages.Should().Be(2);
            evt.ExpectedRecords.Should().Be(4);
            return true;
        });

        probe.ExpectMsg<IngestionStatusResponse>(msg => msg.State == "Completed");
        eventProbe.ExpectMsg<DataCollectionCompleted>(evt => evt.TotalRecords == 4);
    }

    private sealed class FakeDataApiClient : IDataApiClient
    {
        private readonly int _pageSize;
        private readonly int _totalPages;

        public FakeDataApiClient(int pageSize = 3, int totalPages = 2)
        {
            _pageSize = pageSize;
            _totalPages = totalPages;
        }

        public Task<ApiDataPage> FetchPageAsync(int pageNumber, CancellationToken cancellationToken = default)
        {
            var records = Enumerable.Range(1, _pageSize)
                .Select(i => new ExternalDataRecord($"p{pageNumber}-i{i}", pageNumber * 10 + i, "fake"))
                .ToList();

            return Task.FromResult(new ApiDataPage(pageNumber, _totalPages, records));
        }
    }
}
