using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using AkkaTeach.Core.Clients;
using AkkaTeach.Worker;
using AkkaTeach.Worker.Clients;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AkkaTeach.Tests.Phase7_Hosting;

public sealed class AkkaHostingRegistrationTests(ITestOutputHelper output) : TeachingTestKit(output)
{
    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.Configure<DataIngestionOptions>(options =>
        {
            options.WorkerPoolSize = 2;
            options.PageSize = 5;
            options.TotalPages = 2;
            options.FetchDelayMilliseconds = 0;
        });
        services.AddSingleton<IDataApiClient, MockDataApiClient>();
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        base.ConfigureAkka(builder, provider);

        builder.AddAkkaTeachActors();
    }

    [Fact]
    public async Task RegisteredActors_AreResolvableFromRegistry()
    {
        var coordinator = await ActorRegistry.GetAsync<WorkCoordinatorActor>();
        var session = await ActorRegistry.GetAsync<SessionActor>();

        coordinator.Should().NotBeNull();
        session.Should().NotBeNull();

        var probe = CreateTestProbe();
        coordinator.Tell(new ProcessWorkItemCommand("hosted", 7), probe.Ref);
        probe.ExpectMsg<WorkItemProcessed>(msg => msg.Result == 14);
    }
}
