using Akka.Actor;
using Akka.Event;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// One-shot helper that introduces every peer to every other peer (full mesh).
/// </summary>
/// <remarks>
/// Shows how actors get each other's addresses at startup — after this, they communicate peer-to-peer
/// with no coordinator in the message path.
/// </remarks>
public sealed class PeerIntroducerActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public PeerIntroducerActor()
    {
        Receive<WirePeerNetworkCommand>(command =>
        {
            _log.Info("Wiring peer network with {Count} actors", command.Peers.Count);
            IntroduceAll(command.Peers);
        });
    }

    public static Props Props() => Akka.Actor.Props.Create<PeerIntroducerActor>();

    /// <summary>
    /// Wires a peer network from tests or bootstrap code outside the actor system.
    /// </summary>
    public static void WireNetwork(IActorRef introducer, params PeerRegistration[] peers) =>
        introducer.Tell(new WirePeerNetworkCommand(peers));

    /// <summary>
    /// Introduces every peer to every other peer directly (used in tests to avoid timing issues).
    /// </summary>
    public static void WirePeersDirectly(params PeerRegistration[] peers) => IntroduceAll(peers);

    private static void IntroduceAll(IReadOnlyList<PeerRegistration> peers)
    {
        foreach (PeerRegistration peer in peers)
        {
            foreach (PeerRegistration other in peers)
            {
                if (peer.Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                peer.Ref.Tell(new IntroducePeerCommand(other.Name, other.Ref));
            }
        }
    }
}
