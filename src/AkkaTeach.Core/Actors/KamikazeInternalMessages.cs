namespace AkkaTeach.Core.Actors;

/// <summary>
/// Worker → manager message when risky work finished cleanly before self-termination.
/// </summary>
internal sealed record KamikazeWorkCompleted(string TaskId, string Result);

/// <summary>
/// Internal kick-off message for the worker's single execution attempt.
/// </summary>
internal sealed record ExecuteKamikazeWork
{
    public static ExecuteKamikazeWork Instance { get; } = new();

    private ExecuteKamikazeWork()
    {
    }
}
