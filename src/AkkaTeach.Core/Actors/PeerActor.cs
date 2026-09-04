using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// A peer in the actor network. Once introduced to other peers, it can message any of them directly.
/// </summary>
/// <remarks>
/// <para><b>Key idea:</b> In Akka, <em>any actor can message any other actor</em> — there is no
/// requirement that they are parent/child. You only need the other actor's <see cref="IActorRef"/>.</para>
/// <para>This actor keeps a simple address book (<c>name → IActorRef</c>). After introduction,
/// <c>SendPeerMessageCommand</c> uses <c>Tell</c> to reach a peer directly across the system.</para>
/// </remarks>
public sealed class PeerActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly string _name;
    private readonly Dictionary<string, IActorRef> _peers = new(StringComparer.OrdinalIgnoreCase);

    public PeerActor(string name)
    {
        _name = name;

        Receive<IntroducePeerCommand>(command =>
        {
            _peers[command.PeerName] = command.PeerRef;
            _log.Debug("{Name} now knows peer {Peer} at {Path}", _name, command.PeerName, command.PeerRef.Path);
        });

        Receive<SendPeerMessageCommand>(command =>
        {
            if (!_peers.TryGetValue(command.TargetName, out IActorRef? peer))
            {
                _log.Warning("{Name} does not know peer {Target}", _name, command.TargetName);
                if (!Sender.IsNobody())
                {
                    Sender.Tell(new KnownPeersResponse(_peers.Keys.ToList()));
                }

                return;
            }

            _log.Info("{From} -> {To}: {Text}", _name, command.TargetName, command.Text);
            peer.Tell(new PeerMessageReceived(_name, command.Text));
        });

        Receive<PeerMessageReceived>(message =>
        {
            _log.Info("{Name} received from {From}: {Text}", _name, message.From, message.Text);
            Context.System.EventStream.Publish(new PeerMessageDelivered(_name, message.From, message.Text));
        });

        Receive<GetKnownPeersQuery>(_ =>
            Sender.Tell(new KnownPeersResponse(_peers.Keys.OrderBy(n => n).ToList())));
    }

    public static Props Props(string name) => Akka.Actor.Props.Create(() => new PeerActor(name));
}
