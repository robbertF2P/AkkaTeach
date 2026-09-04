using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Phase3_Messaging;

/// <summary>
/// Proves that sibling peers can message each other directly — no parent/child routing required.
/// </summary>
public sealed class PeerActorTests(ITestOutputHelper output) : TeachingTestKit(output)
{

    [Fact]
    public void AnyPeer_CanMessageAnyOtherPeer_DirectlyViaIActorRef()
    {
        var alice = Sys.ActorOf(PeerActor.Props("Alice"), "alice");
        var bob = Sys.ActorOf(PeerActor.Props("Bob"), "bob");
        var carol = Sys.ActorOf(PeerActor.Props("Carol"), "carol");
        var eventProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(PeerMessageDelivered));
        WirePeers(
            new PeerRegistration("Alice", alice),
            new PeerRegistration("Bob", bob),
            new PeerRegistration("Carol", carol));

        WaitUntilPeerKnows(alice, "Bob", "Carol");

        // Alice messages Bob directly — no shared parent, no forward, no coordinator in the path.
        alice.Tell(new SendPeerMessageCommand("Bob", "Hello Bob"));

        eventProbe.ExpectMsg<PeerMessageDelivered>(msg =>
        {
            msg.To.Should().Be("Bob");
            msg.From.Should().Be("Alice");
            msg.Text.Should().Be("Hello Bob");
            return true;
        });
    }

    [Fact]
    public void Peers_CanMessageAcrossTheMesh_WithoutSharedParent()
    {
        var alice = Sys.ActorOf(PeerActor.Props("Alice"), "alice");
        var bob = Sys.ActorOf(PeerActor.Props("Bob"), "bob");
        var carol = Sys.ActorOf(PeerActor.Props("Carol"), "carol");
        var eventProbe = CreateTestProbe();

        Sys.EventStream.Subscribe(eventProbe.Ref, typeof(PeerMessageDelivered));
        WirePeers(
            new PeerRegistration("Alice", alice),
            new PeerRegistration("Bob", bob),
            new PeerRegistration("Carol", carol));

        WaitUntilPeerKnows(bob, "Alice", "Carol");

        // Bob messages Carol — neither is parent of the other.
        bob.Tell(new SendPeerMessageCommand("Carol", "Hey Carol"));

        eventProbe.ExpectMsg<PeerMessageDelivered>(msg =>
        {
            msg.To.Should().Be("Carol");
            msg.From.Should().Be("Bob");
            msg.Text.Should().Be("Hey Carol");
            return true;
        });
    }

    [Fact]
    public void Peer_KnowsEveryOtherPeer_AfterFullMeshWiring()
    {
        var alice = Sys.ActorOf(PeerActor.Props("Alice"), "alice");
        var bob = Sys.ActorOf(PeerActor.Props("Bob"), "bob");
        var carol = Sys.ActorOf(PeerActor.Props("Carol"), "carol");

        WirePeers(
            new PeerRegistration("Alice", alice),
            new PeerRegistration("Bob", bob),
            new PeerRegistration("Carol", carol));

        WaitUntilPeerKnows(alice, "Bob", "Carol");
    }

    private void WirePeers(params PeerRegistration[] peers) =>
        PeerIntroducerActor.WirePeersDirectly(peers);

    private void WaitUntilPeerKnows(IActorRef peer, params string[] expectedPeers)
    {
        var probe = CreateTestProbe();
        peer.Tell(new GetKnownPeersQuery(), probe.Ref);
        probe.ExpectMsg<KnownPeersResponse>(
            msg =>
            {
                msg.PeerNames.Should().BeEquivalentTo(expectedPeers);
                return true;
            },
            timeout: TimeSpan.FromSeconds(3));
    }
}
