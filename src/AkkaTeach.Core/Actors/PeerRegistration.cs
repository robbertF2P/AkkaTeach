using Akka.Actor;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Name + address pair used when bootstrapping a peer network.
/// </summary>
public sealed record PeerRegistration(string Name, IActorRef Ref);
