using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Clients;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Minimal actor that demonstrates <c>PipeTo</c>.
/// </summary>
/// <remarks>
/// <para><b>Problem:</b> Actors must not block inside <c>Receive</c> handlers
/// (<c>.Result</c>, <c>.Wait()</c>, <c>Thread.Sleep</c>).</para>
/// <para><b>Solution:</b> Start the async call, then <c>PipeTo(Self, ...)</c> so the
/// result arrives as a normal mailbox message when the task completes.</para>
/// <para><b>Proof:</b> While in the <c>Fetching</c> behavior, <see cref="GetFetchStatusQuery"/>
/// still gets a reply — the actor was never blocked waiting on I/O.</para>
/// </remarks>
public sealed class PipeToDemoActor : ReceiveActor
{
    private readonly IQuoteService _quoteService;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private IActorRef? _requester;
    private string _topic = string.Empty;

    public PipeToDemoActor(IQuoteService quoteService)
    {
        _quoteService = quoteService;
        Become(Idle);
    }

    private void Idle()
    {
        Receive<FetchQuoteCommand>(command =>
        {
            _requester = Sender.IsNobody() ? null : Sender;
            _topic = command.Topic;
            _log.Info("Starting async fetch for topic {Topic}", _topic);

            // PipeTo: kick off async I/O, return immediately, handle result as a message later.
            // Do NOT write: var quote = _quoteService.FetchQuoteAsync(...).Result;
            _quoteService.FetchQuoteAsync(command.Topic).PipeTo(
                Self,
                Self,
                success: quote => new QuoteFetched(quote),
                failure: ex => new QuoteFetchFailed(ex));

            Become(Fetching);
        });

        Receive<GetFetchStatusQuery>(_ => Sender.Tell(new FetchStatusResponse("Idle")));
    }

    private void Fetching()
    {
        // This handler proves the mailbox stayed open while the Task was running.
        Receive<GetFetchStatusQuery>(_ => Sender.Tell(new FetchStatusResponse("Fetching")));

        Receive<QuoteFetched>(message =>
        {
            _log.Info("Async fetch completed for topic {Topic}", _topic);
            _requester?.Tell(new QuoteFetchedResponse(_topic, message.Quote));
            _requester = null;
            Become(Idle);
        });

        Receive<QuoteFetchFailed>(failure =>
        {
            _log.Error(failure.Exception, "Async fetch failed for topic {Topic}", _topic);
            _requester?.Tell(new FetchStatusResponse("Failed"));
            _requester = null;
            Become(Idle);
        });
    }

    private sealed record QuoteFetched(string Quote);

    private sealed record QuoteFetchFailed(Exception Exception);

    public static Props Props(IQuoteService quoteService) =>
        Akka.Actor.Props.Create(() => new PipeToDemoActor(quoteService));
}
