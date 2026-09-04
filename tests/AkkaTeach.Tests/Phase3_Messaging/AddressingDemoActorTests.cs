using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Phase3_Messaging;

public sealed class AddressingDemoActorTests(ITestOutputHelper output) : TeachingTestKit(output)
{

    [Fact]
    public void DirectTell_WithExplicitSender_ReplyGoesToCaller()
    {
        var greeter = Sys.ActorOf(GreeterActor.Props(), "greeter");
        var probe = CreateTestProbe();

        greeter.Tell(new SayHelloCommand("Alice"), probe.Ref);

        probe.ExpectMsg<HelloReply>(msg => msg.Message == "Hello, Alice!");
    }

    [Fact]
    public void AskViaTell_FrontDeskPassesSender_GreeterSeesOriginalCaller()
    {
        var greeter = Sys.ActorOf(SenderReportingGreeterActor.Props(), "greeter");
        var frontDesk = Sys.ActorOf(AddressingDemoActor.Props(greeter), "front-desk");
        var clientProbe = CreateTestProbe("client");

        frontDesk.Tell(new AskViaTellCommand("Bob"), clientProbe.Ref);

        clientProbe.ExpectMsg<HelloReply>(msg =>
        {
            msg.Message.Should().StartWith("Bob:");
            msg.Message.Should().Contain(clientProbe.Ref.Path.ToString());
            return true;
        });
    }

    [Fact]
    public void AskViaForward_GreeterSeesOriginalCaller_NotMiddleman()
    {
        var greeter = Sys.ActorOf(SenderReportingGreeterActor.Props(), "greeter");
        var frontDesk = Sys.ActorOf(AddressingDemoActor.Props(greeter), "front-desk");
        var clientProbe = CreateTestProbe("client");

        frontDesk.Tell(new AskViaForwardCommand("Carol"), clientProbe.Ref);

        clientProbe.ExpectMsg<HelloReply>(msg =>
        {
            msg.Message.Should().StartWith("Carol:");
            msg.Message.Should().Contain(clientProbe.Ref.Path.ToString());
            msg.Message.Should().NotContain("front-desk");
            return true;
        });
    }

    [Fact]
    public void ChildGreeter_IsAddressedViaFieldSetByActorOf()
    {
        var frontDesk = Sys.ActorOf(AddressingDemoActor.Props(), "front-desk-with-child");
        var probe = CreateTestProbe();

        frontDesk.Tell(new AskViaTellCommand("Dave"), probe.Ref);

        probe.ExpectMsg<HelloReply>(msg => msg.Message == "Hello, Dave!");
    }

    /// <summary>
    /// Test-only greeter that embeds the sender path in the reply so we can verify addressing.
    /// </summary>
    private sealed class SenderReportingGreeterActor : ReceiveActor
    {
        public SenderReportingGreeterActor()
        {
            Receive<SayHelloCommand>(command =>
                Sender.Tell(new HelloReply($"{command.Name}:{Sender.Path}")));
        }

        public static Props Props() => Akka.Actor.Props.Create<SenderReportingGreeterActor>();
    }
}
