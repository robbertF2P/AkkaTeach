using Akka.Actor;
using Akka.Hosting;
using AkkaTeach.Core.Actors;
using AkkaTeach.Core.Clients;
using AkkaTeach.Worker.Clients;

namespace AkkaTeach.Worker;

public static class AkkaHostingExtensions
{
    public const string WorkCoordinatorActorName = "work-coordinator";
    public const string SessionActorName = "session";
    public const string DataIngestionActorName = "data-ingestion";

    public static AkkaConfigurationBuilder AddAkkaTeachActors(this AkkaConfigurationBuilder builder)
    {
        return builder.WithActors((system, registry, resolver) =>
        {
            IActorRef coordinator = system.ActorOf(WorkCoordinatorActor.Props(), WorkCoordinatorActorName);
            IActorRef session = system.ActorOf(SessionActor.Props(), SessionActorName);
            IActorRef ingestion = system.ActorOf(resolver.Props<DataIngestionActor>(), DataIngestionActorName);

            registry.Register<WorkCoordinatorActor>(coordinator);
            registry.Register<SessionActor>(session);
            registry.Register<DataIngestionActor>(ingestion);
        });
    }

    public static IServiceCollection AddAkkaTeachActors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DataIngestionOptions>(configuration.GetSection(DataIngestionOptions.SectionName));
        services.AddSingleton<IDataApiClient, MockDataApiClient>();
        services.AddAkka("AkkaTeach", (builder, _) => builder.AddAkkaTeachActors());
        return services;
    }
}
