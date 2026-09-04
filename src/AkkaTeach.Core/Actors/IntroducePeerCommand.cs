using Akka.Actor;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Registers another actor's address so this peer can message it later.
/// </summary>
internal sealed record IntroducePeerCommand(string PeerName, IActorRef PeerRef);
