using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Teaching actor that switches behavior with <c>Become</c>:
/// Idle -> Active -> Completed.
/// </summary>
public sealed class SessionActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private string? _sessionId;
    private int _stepsRecorded;

    public SessionActor()
    {
        Become(Idle);
    }

    private static void ReplyIfAsked(IActorRef sender, object message)
    {
        if (!sender.IsNobody())
        {
            sender.Tell(message);
        }
    }

    private void Idle()
    {
        Receive<StartSessionCommand>(command =>
        {
            _sessionId = command.SessionId;
            _stepsRecorded = 0;
            _log.Info("Session {SessionId} started", _sessionId);
            Context.System.EventStream.Publish(new SessionStarted(_sessionId));
            ReplyIfAsked(Sender, new SessionStateResponse("Active", _sessionId, _stepsRecorded));
            Become(Active);
        });

        Receive<GetSessionStateQuery>(_ => Sender.Tell(new SessionStateResponse("Idle", null, 0)));
        ReceiveAny(msg => _log.Warning("Ignoring {MessageType} while Idle", msg.GetType().Name));
    }

    private void Active()
    {
        Receive<RecordProgressCommand>(command =>
        {
            _stepsRecorded = command.Step;
            _log.Debug("Session {SessionId} progress: step {Step}", _sessionId, _stepsRecorded);
            ReplyIfAsked(Sender, new SessionStateResponse("Active", _sessionId, _stepsRecorded));
        });

        Receive<EndSessionCommand>(_ =>
        {
            string sessionId = _sessionId!;
            int totalSteps = _stepsRecorded;
            _log.Info("Session {SessionId} ended after {Steps} steps", sessionId, totalSteps);
            Context.System.EventStream.Publish(new SessionEnded(sessionId, totalSteps));
            ReplyIfAsked(Sender, new SessionStateResponse("Completed", sessionId, totalSteps));
            Become(Completed);
        });

        Receive<GetSessionStateQuery>(_ => Sender.Tell(new SessionStateResponse("Active", _sessionId, _stepsRecorded)));
        ReceiveAny(msg => _log.Warning("Ignoring {MessageType} while Active", msg.GetType().Name));
    }

    private void Completed()
    {
        Receive<ResetSessionCommand>(_ =>
        {
            _log.Debug("Session {SessionId} reset to idle", _sessionId);
            _sessionId = null;
            _stepsRecorded = 0;
            ReplyIfAsked(Sender, new SessionStateResponse("Idle", null, 0));
            Become(Idle);
        });

        Receive<GetSessionStateQuery>(_ => Sender.Tell(new SessionStateResponse("Completed", _sessionId, _stepsRecorded)));
        ReceiveAny(msg => _log.Warning("Session {SessionId} is completed; ignoring {MessageType}", _sessionId, msg.GetType().Name));
    }

    public static Props Props() => Akka.Actor.Props.Create<SessionActor>();
}
