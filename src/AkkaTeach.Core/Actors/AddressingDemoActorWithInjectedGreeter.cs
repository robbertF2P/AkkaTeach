using Akka.Actor;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Same routing logic as <see cref="AddressingDemoActor"/>, but the greeter address is passed in
/// via constructor instead of being created as a child.
/// </summary>
internal sealed class AddressingDemoActorWithInjectedGreeter : ReceiveActor
{
    private readonly IActorRef _greeter;

    public AddressingDemoActorWithInjectedGreeter(IActorRef greeter)
    {
        _greeter = greeter;

        Receive<AskViaTellCommand>(command =>
            _greeter.Tell(new SayHelloCommand(command.Name), Sender));

        Receive<AskViaForwardCommand>(command =>
            _greeter.Forward(new SayHelloCommand(command.Name)));
    }
}
