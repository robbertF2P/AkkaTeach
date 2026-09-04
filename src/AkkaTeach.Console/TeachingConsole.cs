using Akka.Actor;
using Akka.Hosting;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using AkkaTeach.Core.Clients;
using Microsoft.Extensions.Options;

namespace AkkaTeach.ConsoleApp;

/// <summary>
/// Drives the registered teaching actors from console commands.
/// </summary>
public sealed class TeachingConsole
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(30);

    private readonly IActorRef _coordinator;
    private readonly IActorRef _session;
    private readonly IActorRef _ingestion;
    private readonly DataIngestionOptions _options;

    public TeachingConsole(IActorRegistry registry, IOptions<DataIngestionOptions> options)
    {
        _coordinator = registry.Get<WorkCoordinatorActor>();
        _session = registry.Get<SessionActor>();
        _ingestion = registry.Get<DataIngestionActor>();
        _options = options.Value;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        PrintBanner();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("akka> ");
            string? line = Console.ReadLine();
            if (line is null)
            {
                return;
            }

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            if (parts[0] is "quit" or "exit")
            {
                return;
            }

            try
            {
                await ExecuteAsync(parts, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  error: {ex.Message}");
            }
        }
    }

    private async Task ExecuteAsync(string[] parts, CancellationToken cancellationToken)
    {
        switch (parts[0])
        {
            case "help":
                PrintHelp();
                return;

            case "work":
                await RunWorkAsync(parts, cancellationToken);
                return;

            case "session":
                await RunSessionAsync(parts, cancellationToken);
                return;

            case "ingest":
                await RunIngestAsync(parts, cancellationToken);
                return;

            case "status":
                await PrintStatusAsync(cancellationToken);
                return;

            default:
                Console.WriteLine($"  unknown command '{parts[0]}' — type 'help'");
                return;
        }
    }

    private async Task RunWorkAsync(string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out int payload))
        {
            Console.WriteLine("  usage: work <itemId> <number>");
            return;
        }

        WorkItemProcessed result = await _coordinator.Ask<WorkItemProcessed>(
            new ProcessWorkItemCommand(parts[1], payload),
            AskTimeout,
            cancellationToken);

        Console.WriteLine($"  processed {result.ItemId} -> {result.Result} (parent forwarded the child's reply)");
    }

    private async Task RunSessionAsync(string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("  usage: session <start|step|end|reset|state> [argument]");
            return;
        }

        IActorSystemMessage command;
        switch (parts[1])
        {
            case "start":
                string sessionId = parts.Length > 2 ? parts[2] : $"session-{Guid.NewGuid():N}"[..16];
                command = new StartSessionCommand(sessionId);
                break;

            case "step":
                if (parts.Length < 3 || !int.TryParse(parts[2], out int step))
                {
                    Console.WriteLine("  usage: session step <number>");
                    return;
                }

                command = new RecordProgressCommand(step);
                break;

            case "end":
                command = new EndSessionCommand();
                break;

            case "reset":
                command = new ResetSessionCommand();
                break;

            case "state":
                command = new GetSessionStateQuery();
                break;

            default:
                Console.WriteLine($"  unknown session command '{parts[1]}'");
                return;
        }

        SessionStateResponse state = await _session.Ask<SessionStateResponse>(command, AskTimeout, cancellationToken);
        Console.WriteLine($"  session state: {state.State} (id: {state.SessionId ?? "-"}, steps: {state.StepsRecorded})");
    }

    private async Task RunIngestAsync(string[] parts, CancellationToken cancellationToken)
    {
        string? collectionId = parts.Length > 1 ? parts[1] : null;
        Console.WriteLine(
            $"  collecting {_options.TotalPages} pages x {_options.PageSize} records over {_options.WorkerPoolSize} pooled workers...");

        IngestionStatusResponse status = await _ingestion.Ask<IngestionStatusResponse>(
            new CollectDataCommand(collectionId),
            AskTimeout,
            cancellationToken);

        Console.WriteLine(
            $"  ingestion {status.State}: {status.PagesCollected} pages, {status.RecordsProcessed}/{status.TotalRecords} records");
    }

    private async Task PrintStatusAsync(CancellationToken cancellationToken)
    {
        CompletedCountResponse completed = await _coordinator.Ask<CompletedCountResponse>(
            new GetCompletedCountQuery(),
            AskTimeout,
            cancellationToken);
        SessionStateResponse session = await _session.Ask<SessionStateResponse>(
            new GetSessionStateQuery(),
            AskTimeout,
            cancellationToken);
        IngestionStatusResponse ingestion = await _ingestion.Ask<IngestionStatusResponse>(
            new GetIngestionStatusQuery(),
            AskTimeout,
            cancellationToken);

        Console.WriteLine($"  work items completed : {completed.Count}");
        Console.WriteLine($"  session              : {session.State} (steps: {session.StepsRecorded})");
        Console.WriteLine($"  ingestion            : {ingestion.State} ({ingestion.RecordsProcessed} records processed)");
    }

    private static void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("AkkaTeach console — drive the teaching actors interactively.");
        PrintHelp();
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("  work <itemId> <number>   parent/child routing + reply forwarding");
        Console.WriteLine("  session start [id]       behavior switch: Idle -> Active");
        Console.WriteLine("  session step <number>    record progress while Active");
        Console.WriteLine("  session end              Active -> Completed");
        Console.WriteLine("  session reset            Completed -> Idle");
        Console.WriteLine("  session state            query current behavior");
        Console.WriteLine("  ingest [collectionId]    paginated fetch (PipeTo) + round-robin worker pool");
        Console.WriteLine("  status                   snapshot of all three actors");
        Console.WriteLine("  help | quit");
        Console.WriteLine();
    }
}
