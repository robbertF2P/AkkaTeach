using Akka.Actor;
using Akka.Event;
using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Waits for dependencies, stashes early work, then <c>UnstashAll</c> when ready.
/// </summary>
/// <remarks>
/// <para><b>Stash pattern:</b> messages arrive before the actor can handle them (warm-up,
/// loading refs, PipeTo in flight). <c>Stash.Stash()</c> buffers them; <c>UnstashAll()</c>
/// prepends them to the mailbox in original order when behavior switches.</para>
/// <para>See guide §10.</para>
/// </remarks>
public sealed class StashGateActor : ReceiveActor, IWithUnboundedStash, IWithTimers
{
    private const string InitTimerKey = "stash-gate-init";

    private readonly ILoggingAdapter _log = Context.GetLogger();

    private string? _gateId;
    private int _stashedCount;
    private int _processedCount;

    public IStash Stash { get; set; } = null!;

    public ITimerScheduler Timers { get; set; } = null!;

    public StashGateActor()
    {
        Become(WaitingForDependencies);
    }

    private void WaitingForDependencies()
    {
        Receive<PrepareStashGateCommand>(command =>
        {
            _gateId = command.GateId;
            _stashedCount = 0;
            _processedCount = 0;

            _log.Info("Gate {GateId} preparing; init delay {Delay}", _gateId, command.InitDelay);
            Timers.StartSingleTimer(InitTimerKey, GateDependenciesLoaded.Instance, command.InitDelay);
        });

        Receive<ProcessGatedItemCommand>(command =>
        {
            _stashedCount++;
            _log.Debug("Stashing item {ItemId} while gate {GateId} is waiting", command.ItemId, _gateId ?? "unknown");
            Stash.Stash();
        });

        Receive<GateDependenciesLoaded>(_ => OpenGate());

        Receive<GetStashGateStatusQuery>(_ =>
            Sender.Tell(new StashGateStatusResponse("Waiting", _gateId, _stashedCount, _processedCount)));
    }

    private void Ready()
    {
        Receive<ProcessGatedItemCommand>(command =>
        {
            _processedCount++;
            var result = $"processed-{_gateId}-{command.ItemId}";
            _log.Debug("Processed gated item {ItemId}", command.ItemId);
            Sender.Tell(new GatedItemProcessed(command.ItemId, result));
        });

        Receive<GetStashGateStatusQuery>(_ =>
            Sender.Tell(new StashGateStatusResponse("Ready", _gateId, 0, _processedCount)));
    }

    private void OpenGate()
    {
        var stashed = _stashedCount;
        _log.Info("Gate {GateId} ready — unstashing {Count} item(s)", _gateId, stashed);
        Context.System.EventStream.Publish(new StashGateOpened(_gateId!, stashed));
        Become(Ready);
        Stash.UnstashAll();
        _stashedCount = 0;
    }

    protected override void PostStop() => Timers.CancelAll();

    public static Props Props() => Akka.Actor.Props.Create<StashGateActor>();
}

/// <summary>
/// Internal timer message when dependency warm-up completes.
/// </summary>
internal sealed record GateDependenciesLoaded
{
    public static GateDependenciesLoaded Instance { get; } = new();

    private GateDependenciesLoaded()
    {
    }
}
