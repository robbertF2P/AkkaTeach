using Akka.Actor;
using Akka.Event;

namespace AkkaTeach.Core.Actors;

/// <summary>
/// Short-lived executor actor: run one risky operation, report to the parent, then stop.
/// </summary>
/// <remarks>
/// <para><b>Kamikaze pattern:</b> spawn a child for dangerous or stateless work. The child owns
/// the failure domain; the parent keeps long-lived state and uses <see cref="Context.Watch"/>
/// to detect crashes before a result arrives.</para>
/// <para>See <c>docs/actor-model-guide.md</c> §15 and <see cref="KamikazeManagerActor"/>.</para>
/// </remarks>
public sealed class KamikazeWorkerActor : ReceiveActor
{
    private readonly string _taskId;
    private readonly bool _shouldFail;
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public KamikazeWorkerActor(string taskId, bool shouldFail)
    {
        _taskId = taskId;
        _shouldFail = shouldFail;

        Receive<ExecuteKamikazeWork>(_ => Execute());
    }

    protected override void PreStart()
    {
        Self.Tell(ExecuteKamikazeWork.Instance);
    }

    private void Execute()
    {
        _log.Debug("Kamikaze worker starting task {TaskId}", _taskId);

        if (_shouldFail)
        {
            throw new InvalidOperationException($"Simulated failure for task {_taskId}");
        }

        Context.Parent.Tell(new KamikazeWorkCompleted(_taskId, $"Result for {_taskId}"));
        Context.Stop(Self);
    }

    public static Props Props(string taskId, bool shouldFail) =>
        Akka.Actor.Props.Create(() => new KamikazeWorkerActor(taskId, shouldFail));
}
