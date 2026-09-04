using Akka.Actor;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Reply to <see cref="Contracts.SpawnChildCommand"/> carrying the supervised child's reference.
/// </summary>
/// <remarks>Lives in Core rather than Contracts because it carries an <see cref="IActorRef"/>,
/// and Contracts deliberately has no Akka dependency.</remarks>
public sealed record ChildSpawnedResponse(IActorRef Child);
