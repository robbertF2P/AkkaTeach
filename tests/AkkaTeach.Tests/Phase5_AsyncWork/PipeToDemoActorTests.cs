using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using AkkaTeach.Core.Clients;
using FluentAssertions;

namespace AkkaTeach.Tests.Phase5_AsyncWork;

/// <summary>
/// Tests that prove <see cref="PipeToDemoActor"/> stays responsive during async I/O.
/// </summary>
public sealed class PipeToDemoActorTests(ITestOutputHelper output) : TeachingTestKit(output)
{

    [Fact]
    public void FetchQuote_UsesPipeTo_MailboxStaysResponsiveWhileWaiting()
    {
        var slowService = new SlowQuoteService(TimeSpan.FromMilliseconds(400));
        var actor = Sys.ActorOf(PipeToDemoActor.Props(slowService), "pipe-to-demo");
        var replyProbe = CreateTestProbe();
        var statusProbe = CreateTestProbe();

        actor.Tell(new FetchQuoteCommand("akka"), replyProbe.Ref);

        // If the actor blocked on .Result/.Wait(), this would time out.
        actor.Tell(new GetFetchStatusQuery(), statusProbe.Ref);
        statusProbe.ExpectMsg<FetchStatusResponse>(msg => msg.State == "Fetching");

        replyProbe.ExpectMsg<QuoteFetchedResponse>(msg =>
        {
            msg.Topic.Should().Be("akka");
            msg.Quote.Should().Contain("akka");
            return true;
        });
    }

    [Fact]
    public void FetchQuote_WhenServiceFails_ReportsFailedStatus()
    {
        var failingService = new FailingQuoteService();
        var actor = Sys.ActorOf(PipeToDemoActor.Props(failingService), "pipe-to-demo-fail");
        var replyProbe = CreateTestProbe();

        actor.Tell(new FetchQuoteCommand("error"), replyProbe.Ref);
        replyProbe.ExpectMsg<FetchStatusResponse>(msg => msg.State == "Failed");
    }

    private sealed class SlowQuoteService : IQuoteService
    {
        private readonly TimeSpan _delay;

        public SlowQuoteService(TimeSpan delay) => _delay = delay;

        public async Task<string> FetchQuoteAsync(string topic, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delay, cancellationToken);
            return $"Quote about {topic} (fetched after delay)";
        }
    }

    private sealed class FailingQuoteService : IQuoteService
    {
        public Task<string> FetchQuoteAsync(string topic, CancellationToken cancellationToken = default) =>
            Task.FromException<string>(new InvalidOperationException("Service unavailable"));
    }
}
