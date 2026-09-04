using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Simple target actor. Other actors address it via its <see cref="IActorRef"/>.
/// </summary>
public sealed class GreeterActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public GreeterActor()
    {
        Receive<SayHelloCommand>(command =>
        {
            _log.Info("Greeter received hello for {Name} from {Sender}", command.Name, Sender.Path);
            Sender.Tell(new HelloReply($"Hello, {command.Name}!"));
        });
    }

    public static Props Props() => Akka.Actor.Props.Create<GreeterActor>();
}
