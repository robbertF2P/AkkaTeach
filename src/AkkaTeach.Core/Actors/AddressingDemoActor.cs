using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Demonstrates how to address another actor.
/// </summary>
/// <remarks>
/// <para><b>IActorRef is the address.</b> You never call methods on another actor directly —
/// you send messages to its <see cref="IActorRef"/>.</para>
/// <para><b>Three ways shown here:</b></para>
/// <list type="number">
/// <item><description><b>Direct Tell</b> — <c>greeter.Tell(message, probe)</c> when you have the ref.</description></item>
/// <item><description><b>Tell with sender</b> — <c>greeter.Tell(message, Sender)</c> so the reply goes to the original caller.</description></item>
/// <item><description><b>Forward</b> — <c>greeter.Forward(message)</c> when you are a middleman; the original sender gets the reply.</description></item>
/// </list>
/// </remarks>
public sealed class AddressingDemoActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    // Child actor — address stored in a field after Context.ActorOf.
    private readonly IActorRef _greeter;

    public AddressingDemoActor()
    {
        _greeter = Context.ActorOf(GreeterActor.Props(), "greeter");

        Receive<AskViaTellCommand>(command =>
        {
            _log.Debug("Front desk routing via Tell to {GreeterPath}", _greeter.Path);

            // Tell the greeter, but set the reply address to the original caller (Sender).
            _greeter.Tell(new SayHelloCommand(command.Name), Sender);
        });

        Receive<AskViaForwardCommand>(command =>
        {
            _log.Debug("Front desk routing via Forward to {GreeterPath}", _greeter.Path);

            // Forward keeps the original Sender — the greeter's reply skips this actor.
            _greeter.Forward(new SayHelloCommand(command.Name));
        });
    }

    public static Props Props() => Akka.Actor.Props.Create<AddressingDemoActor>();

    /// <summary>
    /// Factory for tests that inject a greeter ref instead of creating a child.
    /// Shows that an <see cref="IActorRef"/> can come from anywhere (parent, DI, registry).
    /// </summary>
    public static Props Props(IActorRef greeter) =>
        Akka.Actor.Props.Create(() => new AddressingDemoActorWithInjectedGreeter(greeter));
}
