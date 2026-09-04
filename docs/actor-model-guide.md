# Actor Model Guide (AkkaTeach)

A walkthrough of core actor-model concepts using samples from this repository.
Each section links to a real actor in `AkkaTeach` and the tests that prove the behavior.

## Run the tests

```bash
cd AkkaTeach
dotnet run --project tests/AkkaTeach.Tests
```

---

## 1. What is an actor?

An actor is a lightweight unit that:

- Receives **messages** one at a time (mailbox)
- Runs **logic** in response
- Can **send messages** to other actors
- Holds **private state** (no shared memory)

**File:** `src/AkkaTeach.Core/Actors/GreeterActor.cs`

```csharp
public GreeterActor()
{
    Receive<SayHelloCommand>(command =>
    {
        _log.Info("Greeter received hello for {Name} from {Sender}", command.Name, Sender.Path);
        Sender.Tell(new HelloReply($"Hello, {command.Name}!"));
    });
}
```

**Takeaway:** You never call `greeter.SayHello()` — you `Tell` it a message. The mailbox serializes access, so you do not need locks on actor state.

---

## 2. Addresses: `IActorRef`

Every actor has an address (`IActorRef`). To talk to it, you need that ref.

**File:** `src/AkkaTeach.Core/Actors/AddressingDemoActor.cs`

```csharp
// Child actor — address stored in a field after Context.ActorOf.
private readonly IActorRef _greeter;

public AddressingDemoActor()
{
    _greeter = Context.ActorOf(GreeterActor.Props(), "greeter");
```

**Ways to get an address:**

| Source | Example |
|--------|---------|
| Create a child | `Context.ActorOf(GreeterActor.Props(), "greeter")` |
| Constructor / DI | `AddressingDemoActorWithInjectedGreeter(IActorRef greeter)` |
| Address book | `PeerActor` stores `name → IActorRef` |
| Akka.Hosting registry | `ActorRegistry.GetAsync<DataIngestionActor>()` |

---

## 3. `Tell` — fire and forget

```csharp
greeter.Tell(new SayHelloCommand("Alice"), probe);
//            message                      who gets the reply
```

- **First argument:** the message
- **Second argument (optional):** who should receive the reply (`Sender` from the receiver's point of view)

If you omit the sender, replies go nowhere useful (dead letters).

---

## 4. `Sender` and replying

Inside a handler, `Sender` is whoever sent the current message:

**File:** `src/AkkaTeach.Core/Actors/GreeterActor.cs`

```csharp
Receive<SayHelloCommand>(command =>
{
    Sender.Tell(new HelloReply($"Hello, {command.Name}!"));
});
```

---

## 5. Middleman: `Tell` with `Sender` vs `Forward`

When actor A talks to B on behalf of client C:

**File:** `src/AkkaTeach.Core/Actors/AddressingDemoActor.cs`

### Tell + pass sender

```csharp
Receive<AskViaTellCommand>(command =>
{
    // Reply goes to the original caller, not the front desk.
    _greeter.Tell(new SayHelloCommand(command.Name), Sender);
});
```

### Forward (preserves original sender automatically)

```csharp
Receive<AskViaForwardCommand>(command =>
{
    // Greeter sees the client as Sender; reply skips the front desk.
    _greeter.Forward(new SayHelloCommand(command.Name));
});
```

```
Client ──► Front desk ──Forward──► Greeter
Client ◄────────────────────────── Greeter   (reply skips front desk)
```

**Tests:** `tests/AkkaTeach.Tests/Actors/AddressingDemoActorTests.cs`

---

## 6. Any actor can message any other

No parent/child relationship is required — only an `IActorRef`.

**File:** `src/AkkaTeach.Core/Actors/PeerActor.cs`

```csharp
Receive<SendPeerMessageCommand>(command =>
{
    peer.Tell(new PeerMessageReceived(_name, command.Text));
});
```

Alice, Bob, and Carol are **siblings** under `/user`. Once introduced via `PeerIntroducerActor`, any peer can `Tell` any other directly:

```
/user/alice  ──Tell──►  /user/bob
/user/bob    ──Tell──►  /user/carol
```

**Tests:** `tests/AkkaTeach.Tests/Actors/PeerActorTests.cs`

---

## 7. Parent/child actors

A parent creates children and holds their refs. The parent often supervises children (restarts on failure — not shown in this demo).

**File:** `src/AkkaTeach.Core/Actors/WorkCoordinatorActor.cs`

```csharp
public WorkCoordinatorActor()
{
    _processor = Context.ActorOf(WorkItemProcessorActor.Props(), "processor");
    Become(Active);
}

Receive<ProcessWorkItemCommand>(command =>
{
    _pendingSender = Sender;
    _processor.Tell(command, Self);   // reply comes back to coordinator
    Become(WaitingForResult);
});
```

The coordinator receives the child reply and forwards the result to the original caller.

**Tests:** `tests/AkkaTeach.Tests/Actors/WorkCoordinatorActorTests.cs`

---

## 8. `Become` — switch behavior (state)

Instead of `if (state == ...)` everywhere, swap which messages you handle.

**File:** `src/AkkaTeach.Core/Actors/SessionActor.cs`

```csharp
public SessionActor()
{
    Become(Idle);
}

Receive<StartSessionCommand>(command =>
{
    // ...
    Become(Active);
});

Receive<EndSessionCommand>(_ =>
{
    // ...
    Become(Completed);
});
```

State machine:

```
Idle ──StartSession──► Active ──EndSession──► Completed ──Reset──► Idle
```

Each state only accepts relevant messages; others are ignored or logged.

**Tests:** `tests/AkkaTeach.Tests/Actors/SessionActorTests.cs`

---

## 9. `PipeTo` — async without blocking

**Never block** inside a `Receive` handler (`.Result`, `.Wait()`, `Thread.Sleep`).

**File:** `src/AkkaTeach.Core/Actors/PipeToDemoActor.cs`

```csharp
// PipeTo: kick off async I/O, return immediately, handle result as a message later.
// Do NOT write: var quote = _quoteService.FetchQuoteAsync(...).Result;
_quoteService.FetchQuoteAsync(command.Topic).PipeTo(
    Self,
    Self,
    success: quote => new QuoteFetched(quote),
    failure: ex => new QuoteFetchFailed(ex));

Become(Fetching);
```

Flow:

1. Start async work
2. Return immediately (mailbox stays open)
3. When the task completes, the result arrives as a **normal message**
4. Handle it in the `Fetching` behavior

While fetching, `GetFetchStatusQuery` still returns `"Fetching"` — proof the actor was not blocked.

**Tests:** `tests/AkkaTeach.Tests/Actors/PipeToDemoActorTests.cs`

The same pattern appears in `DataIngestionActor` for paginated API calls:

**File:** `src/AkkaTeach.Core/Actors/DataIngestionActor.cs`

```csharp
_apiClient.FetchPageAsync(nextPage).PipeTo(
    Self,
    Self,
    success: page => new ApiPageReceived(page),
    failure: ex => new ApiPageFetchFailed(ex));
```

---

## 10. Scheduler — delay and repeat messages

Actors often need **“do this later”** or **“do this every N seconds”** — retry after an HTTP failure, debounce rapid saves, periodic sync. Do **not** use `Thread.Sleep` in a handler; that blocks the mailbox.

Akka.NET gives you two APIs. Both schedule ordinary messages that arrive in the mailbox like any other `Tell`.

### Option A — `IWithTimers` (preferred inside actors)

Implement `IWithTimers` on your `ReceiveActor`. Timers are keyed so you can cancel or replace them.

```csharp
public sealed class P6SyncActor : ReceiveActor, IWithTimers
{
    public ITimerScheduler Timers { get; set; } = null!;

    private const string RetryKey = "p6-retry";

    public P6SyncActor()
    {
        Receive<StartP6Sync>(cmd => BeginSync(cmd));

        Receive<P6ExportFailed>(failed =>
        {
            // Retry once in 5 minutes; replaces any existing retry with the same key
            Timers.StartSingleTimer(RetryKey, new StartP6Sync(failed.SyncId), TimeSpan.FromMinutes(5));
        });

        Receive<SyncSucceeded>(_ => Timers.Cancel(RetryKey));
    }

    protected override void PostStop() => Timers.CancelAll();
}
```

| Method | Use |
|--------|-----|
| `StartSingleTimer(key, message, delay)` | Fire **once** after a delay |
| `StartPeriodicTimer(key, message, interval)` | Fire **repeatedly** every interval |
| `Cancel(key)` | Stop one timer |
| `CancelAll()` | Stop all timers (call in `PostStop`) |

Using the same **key** for a new `StartSingleTimer` / `StartPeriodicTimer` **replaces** the previous timer — handy for debouncing (“wait until user stops typing”).

### Option B — `Context.System.Scheduler` (lower level)

Use when you need an `ICancelable` handle outside `IWithTimers`, or from non-actor code that holds an `ActorSystem`.

```csharp
// Once after 5 seconds
Context.System.Scheduler.ScheduleTellOnce(
    TimeSpan.FromSeconds(5),
    Self,
    new RetryExport(syncId),
    Self);

// Every 30 seconds, starting after 30 seconds
var cancel = new Cancelable(Context.System.Scheduler);
Context.System.Scheduler.ScheduleTellRepeatedly(
    TimeSpan.FromSeconds(30),
    TimeSpan.FromSeconds(30),
    Self,
    new PollP6Changes(),
    Self,
    cancel);

// Later: cancel.Cancel();  — stops the repeat
```

| Method | Use |
|--------|-----|
| `ScheduleTellOnce(delay, receiver, message, sender)` | One-shot delayed `Tell` |
| `ScheduleTellRepeatedly(initialDelay, interval, receiver, message, sender, cancelable?)` | Repeating `Tell` |

### How it fits with other patterns

```
HTTP fails (expected)     →  ScheduleTellOnce / StartSingleTimer  →  retry message later
Long HTTP call            →  PipeTo (not scheduler)
Periodic background sync  →  StartPeriodicTimer or Hangfire at host boundary
Actor stops               →  Timers.CancelAll() in PostStop
New sync supersedes old   →  Cancel retry key before starting new work
```

**Floor2Plan / P6 example:** after a transient P6 API error, `P6SyncActor` schedules `StartP6Sync` with backoff instead of blocking or spinning. Scheduled sync from the host can `facade.Tell(StartP6Sync)` on a cron, or an actor can use `StartPeriodicTimer` for in-process polling.

**Rule:** scheduled messages are **not** special — handle them in `Receive` like any other message. Combine with `Become` if only certain states should accept retries.

---

## 11. Routers / worker pools

Fan work out to many workers behind one address.

**File:** `src/AkkaTeach.Core/Actors/DataIngestionActor.cs`

```csharp
_workerPool = Context.ActorOf(
    DataRecordWorkerActor.Props().WithRouter(new RoundRobinPool(_workerPoolSize)),
    "data-worker-pool");

foreach (var record in page.Records)
{
    _workerPool.Tell(new ProcessDataRecordCommand(record), Self);
}
```

You `Tell` the pool; the router picks the next worker. Parallelism without threads or locks in your code.

**Tests:** `tests/AkkaTeach.Tests/Actors/DataIngestionActorTests.cs`

---

## 12. Event stream — pub/sub

Actors can publish events that others subscribe to (loose coupling).

**Files:**

- `src/AkkaTeach.Core/Actors/SessionActor.cs`
- `src/AkkaTeach.Core/Actors/DataIngestionActor.cs`
- `src/AkkaTeach.Core/Actors/PeerActor.cs`

```csharp
Context.System.EventStream.Publish(new SessionStarted(_sessionId));
```

Subscribe in tests or other actors:

```csharp
Sys.EventStream.Subscribe(probe.Ref, typeof(PeerMessageDelivered));
```

Unlike `Tell`, subscribers do not reply — they observe.

**Note:** EventStream is **local to one `ActorSystem` process**. For multi-node clusters, use DistributedPubSub instead — see [From akka-net-best-practices](#from-akka-net-best-practices).

## 13. Keep I/O outside actors

Actors depend on abstractions; real HTTP/DB code lives behind interfaces.

**Files:**

- `src/AkkaTeach.Core/Clients/IDataApiClient.cs` — port
- `src/AkkaTeach.Worker/Clients/MockDataApiClient.cs` — mock implementation

```csharp
// Actor depends on IDataApiClient, not HttpClient directly.
_apiClient.FetchPageAsync(pageNumber).PipeTo(Self, ...);
```

Swap `MockDataApiClient` for a real `HttpClient` implementation later; actors stay unchanged.

---

## Quick reference

| Topic | Actor | Test file |
|-------|-------|-----------|
| Basic messaging + reply | `GreeterActor` | `AddressingDemoActorTests` |
| Tell / Forward / sender | `AddressingDemoActor` | `AddressingDemoActorTests` |
| Any-to-any messaging | `PeerActor` | `PeerActorTests` |
| Parent/child | `WorkCoordinatorActor` | `WorkCoordinatorActorTests` |
| Become / state | `SessionActor` | `SessionActorTests` |
| PipeTo / async I/O | `PipeToDemoActor` | `PipeToDemoActorTests` |
| Scheduler / timers | `IWithTimers`, `ScheduleTellOnce` | See [§10](#10-scheduler--delay-and-repeat-messages) |
| PipeTo in a real flow | `DataIngestionActor` | `DataIngestionActorTests` |
| Worker pools | `DataIngestionActor` | `DataIngestionActorTests` |
| Event stream | `SessionActor`, `PeerActor` | `SessionActorTests`, `PeerActorTests` |
| Akka.Hosting / DI | `AkkaHostingExtensions` | `AkkaHostingRegistrationTests` |

---

## Best practices

Practical guidance for Akka.NET actors. These apply beyond this teaching repo.

### Messaging

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Prefer `Tell` over `Ask` | `Ask` blocks a thread waiting for a reply; actors should stay asynchronous | All actors use `Tell`; `Ask` only at system boundaries (tests, HTTP facades) |
| Never `Ask` inside actors | Deadlocks and thread starvation | `WorkCoordinatorActor` uses `Tell` + `Become` instead |
| Use `Forward` when middleman should not get the reply | Preserves original `Sender` without manual passing | `AddressingDemoActor` |
| Pass `Sender` explicitly when routing replies | Ensures the right caller gets the response | `_greeter.Tell(msg, Sender)` |
| Use fire-and-forget only when no reply is needed | Avoid dead letters | `TeachingBackgroundWorker` uses `Tell` without expecting replies; actors check `Sender.IsNobody()` |

### Message design

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Use immutable records for messages | Thread-safe, easy to reason about | `ProcessWorkItemCommand`, `PeerMessageReceived` in `Contracts/` |
| Separate commands, queries, and events | Clear intent; events are past tense | `SessionStarted` (event) vs `StartSessionCommand` (command) |
| Keep messages small | Large payloads clog mailboxes and serialize poorly | `ExternalDataRecord` carries an ID + value, not a full object graph |
| Put shared messages in a `Contracts` project | Actors in `Core` share a stable API | `AkkaTeach.Contracts` |
| Name events in past tense | Signals something already happened | `DataCollectionCompleted`, `WorkItemCompleted` |

### Actor structure

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Inherit `ReceiveActor` and configure handlers in the constructor | Standard Akka.NET pattern | All actors in `Core/Actors/` |
| Use `sealed class` unless extension is intended | Clear intent | `GreeterActor`, `SessionActor`, etc. |
| Add a static `Props` factory | Centralizes creation; required for DI and tests | `GreeterActor.Props()` |
| Log with `Context.GetLogger()` | Integrates with Akka logging pipeline | `_log = Context.GetLogger()` in every actor |
| Keep actor logic pure; push I/O behind interfaces | Testable, swappable | `IDataApiClient`, `IQuoteService` |

### State and behavior

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Store state in instance fields only | Mailbox serializes access — no locks needed | `_stepsRecorded` in `SessionActor` |
| Do not use `Interlocked` for per-actor state | Unnecessary; mailbox already serializes | — |
| Use `Become` for distinct states | Cleaner than giant `switch` / `if` chains | `SessionActor`: Idle → Active → Completed |
| Ignore or log unexpected messages per state | Fails safe instead of corrupting state | `ReceiveAny` in `SessionActor` |
| Capture `Sender` before async/`Become` if you need it later | `Sender` changes on the next message | `_pendingSender` in `WorkCoordinatorActor` |

### Async and I/O

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Never block in `Receive` handlers | Blocks the mailbox; kills throughput | See `PipeToDemoActor` comments |
| Use `PipeTo(Self, ...)` for `Task`-based I/O | Result arrives as a normal message | `PipeToDemoActor`, `DataIngestionActor` |
| Use `IWithTimers` or `ScheduleTellOnce` for delays/retries | Never `Thread.Sleep` in handlers | See [§10](#10-scheduler--delay-and-repeat-messages); P6 retry backoff |
| Resolve dependencies at `Props` creation time | Avoid service locator inside actors | `DataIngestionActor(IDataApiClient, IOptions<...>)` |
| Do not pass `IServiceProvider` into actors | Hides dependencies; hard to test | Use `Akka.Hosting` `resolver.Props<T>()` instead |

### Addressing and topology

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Store `IActorRef` in a field when reused | Stable address; no repeated lookups | `_greeter`, `_workerPool`, `_peers` |
| Prefer injected refs over `ActorSelection` by path | Paths are fragile; refs are type-safe | `PeerActor` address book vs string paths |
| One `ActorSystem` per application | Expensive to create; designed as singleton | `AkkaHostingExtensions` registers one system |
| Use routers for parallel work | Built-in load distribution | `RoundRobinPool` in `DataIngestionActor` |

### Hosting and DI (modern Akka.NET)

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Use `Akka.Hosting` with `Microsoft.Extensions.DependencyInjection` | Same patterns as ASP.NET Core / worker services | `AddAkkaTeachActors()` |
| Register actors in `WithActors` with stable names | Predictable paths; registry lookup | `"data-ingestion"`, `"work-coordinator"` |
| Reuse production registration in tests | Configuration parity | `AkkaHostingRegistrationTests` |
| Use `Akka.Hosting.TestKit` for actor tests | DI-aware test host | All tests inherit `TestKit` |

### Testing

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Test one actor behavior at a time | Isolates failures | Separate test classes per actor |
| Use `TestProbe` to stand in for other actors | Verify messages sent/received | `AddressingDemoActorTests` |
| Use event stream to observe side effects | Loose coupling verification | `PeerActorTests` + `PeerMessageDelivered` |
| Disable config file watching in test projects | Prevents inotify exhaustion on Linux | `TestEnvironmentInitializer.cs` |
| Prove non-blocking behavior explicitly | Documents `PipeTo` value | `PipeToDemoActorTests` queries status while fetching |

### Supervision and failure

| Practice | Why |
|----------|-----|
| Let parents supervise children in the same feature area | Local failure containment |
| Log exceptions with context; do not swallow silently | `_log.Error(ex, "context")` |
| Design for failure: expect messages to be lost only with dead letters, not crashes | At-least-once semantics need idempotent handlers |
| Use supervision strategies (restart, backoff) in production | Not shown in this demo — defaults are fine for teaching |

### Logging

| Practice | Why | Example in AkkaTeach |
|----------|-----|----------------------|
| Use `ILoggingAdapter` inside actors | Akka-aware, respects log levels | `Context.GetLogger()` |
| Use Serilog for the host | Structured logging outside actors | `Program.cs` |
| Log at `Debug` for message flow, `Info` for business events | Keeps production logs readable | `PeerActor`, `DataIngestionActor` |

---

## Anti-patterns

| Do not | Do instead |
|--------|------------|
| `.Result` / `.Wait()` on async calls | `PipeTo(Self, ...)` |
| `Thread.Sleep` in handlers | `IWithTimers`, `ScheduleTellOnce`, or `PipeTo` |
| `Ask` inside actors | `Tell` + `Become`, or `Forward` |
| Shared mutable static state between actors | Pass data in immutable messages |
| Look up actors by path in application code | Inject `IActorRef`, use `ActorRegistry`, or pass refs at creation |
| Pass `IServiceProvider` into actors | Resolve dependencies when building `Props` |
| Send large object graphs in messages | Send IDs; load data in the receiving actor if needed |
| Create a new `ActorSystem` per request | One hosted singleton system for the app |
| Block the mailbox during I/O | `PipeTo` or message-driven continuation |
| Use `Interlocked` for normal actor state | Trust the mailbox |

---

## From akka-net-best-practices

Items below come from the [akka-net-best-practices](https://github.com/aaronontheweb/dotnet-skills/tree/master/skills/akka-best-practices) skill (Aaronontheweb/dotnet-skills). They extend what this teaching repo demonstrates and matter when you move from single-process demos to clustered production.

### EventStream is local only

Section 11 above uses `Context.System.EventStream` for loose coupling inside one process. That is fine for logging, diagnostics, and single-node apps.

**Critical:** EventStream does **not** cross cluster nodes. A subscriber on node B never sees events published on node A.

| Scenario | Use |
|----------|-----|
| Same process / single server | `EventStream` (as in `SessionActor`, `PeerActor`) |
| Multiple cluster nodes | `Akka.Cluster.Tools.PublishSubscribe` (`DistributedPubSub`) |

For cluster-wide pub/sub, register the mediator via Akka.Hosting (`WithDistributedPubSub`) and publish/subscribe through it — not through `EventStream`.

### Supervision supervises children, not self

A `SupervisorStrategy` on an actor defines how **that actor handles failures in its children**. It does **not** protect the actor itself from crashing.

```
ParentActor (defines strategy)
├── ChildA  ← strategy applies here
└── ChildB  ← and here
```

The parent's own parent supervises the parent. The default `OneForOneStrategy` (10 restarts within 1 second) is usually enough; customize only when you have a concrete reason (e.g. `Resume` for expected transient errors, `AllForOneStrategy` when siblings must restart together).

### Try-catch vs supervision

| Situation | Approach |
|-----------|----------|
| **Expected** failure (HTTP timeout, bad input, external service down) | `try/catch`, log, reply with error, schedule retry |
| **Unknown** failure or possibly corrupt state | Let the exception propagate → supervision restarts |
| **Programming bug** (`NullReferenceException`, bad invariants) | Let supervision restart; fix the code |

Anti-pattern: `catch (Exception)` on everything, log, and continue — the actor may keep running with corrupt state.

### CancellationToken and PostStop for PipeTo

`PipeTo` starts a `Task` that can finish **after** the actor stops or after a newer message supersedes the work. Manage lifecycle explicitly:

1. Hold a `CancellationTokenSource` on the actor.
2. Create a **new** linked CTS per async operation; cancel the previous one when starting new work.
3. Pass the token into HTTP/EF calls.
4. In `PostStop()`, cancel and dispose the CTS so in-flight work does not call `Self.Tell` on a dead actor.

See [async-cancellation-patterns.md](https://github.com/aaronontheweb/dotnet-skills/blob/master/skills/akka-best-practices/async-cancellation-patterns.md) in the skill repo for full patterns.

### Do not inject `ILogger<T>` into actors

Use `ILoggingAdapter` from `Context.GetLogger()` (as every actor in AkkaTeach does). Injected `ILogger<T>` bypasses Akka's logging pipeline and supervision integration. Host apps can still use Serilog outside actors (`Program.cs` in this repo).

### `AkkaExecutionMode` — run the same app with or without a cluster

`AkkaExecutionMode` is **not** a built-in Akka.NET type — it is a **convention** from the best-practices skills for switching infrastructure wiring while keeping entity actors and message types unchanged.

```csharp
public enum AkkaExecutionMode
{
  /// Local: no remoting/cluster. In-memory pub/sub, local "sharding" parent.
  LocalTest,

  /// Production: Cluster Sharding, DistributedPubSub, clustering enabled.
  Clustered
}
```

#### What problem it solves

Cluster features (sharding, distributed pub/sub, cluster singletons) are awkward in unit tests and local dev: you need multiple nodes, slower startup, and more moving parts. Entity actors (`OrderActor`, `SessionActor` per ID, etc.) should not be rewritten for tests.

`AkkaExecutionMode` lets **hosting configuration** pick real cluster machinery or lightweight stand-ins. Application code keeps sending the same messages to the same registry keys / parent refs.

#### The two modes

| Mode | Cluster | Entity routing | Cross-node pub/sub |
|------|---------|----------------|-------------------|
| **LocalTest** | Off | `GenericChildPerEntityParent` | `LocalPubSubMediator` (in-memory) |
| **Clustered** | On | `WithShardRegion<T>` | `ClusterPubSubMediator` → `DistributedPubSub` |

**LocalTest** — one `ActorSystem`, no cluster join. A `GenericChildPerEntityParent` actor:

- Accepts messages (often wrapped in `ShardingEnvelope`)
- Uses the same `IMessageExtractor` as production sharding to get `entityId`
- `GetOrCreate`s a child actor per entity ID and `Forward`s the message

Entity actors see the same message shapes and routing rules as under a real `ShardRegion`; only the parent implementation differs.

**Clustered** — `WithClustering()`, `WithShardRegion<T>()`, `WithDistributedPubSub()`. Shards move between nodes, pub/sub reaches subscribers on other JVM/.NET processes.

#### Wiring pattern (conceptual)

```csharp
public static AkkaConfigurationBuilder WithOrderActors(
    this AkkaConfigurationBuilder builder,
    AkkaExecutionMode mode,
    IServiceCollection services)
{
  if (mode == AkkaExecutionMode.Clustered)
  {
    builder
      .WithClustering()
      .WithShardRegion<OrderActor>(/* ... */, new OrderMessageExtractor(), /* ... */)
      .WithDistributedPubSub();
    services.AddSingleton<IPubSubMediator>(sp => new ClusterPubSubMediator(sp.GetRequiredService<ActorSystem>()));
  }
  else
  {
    services.AddSingleton<IPubSubMediator>(sp => new LocalPubSubMediator(sp.GetRequiredService<ActorSystem>()));
    builder.WithActors((system, registry, resolver) =>
    {
      var parent = system.ActorOf(
        GenericChildPerEntityParent.CreateProps(
          new OrderMessageExtractor(),
          entityId => resolver.Props<OrderActor>(entityId)),
        "orders");
      registry.Register<OrderActor>(parent);
    });
  }
  return builder;
}
```

`IPubSubMediator` is a thin interface (`Subscribe`, `Publish`, `Send`, …) with local and cluster implementations so services do not call `DistributedPubSub` directly.

#### When to use which mode

| Scenario | Mode |
|----------|------|
| Unit tests | LocalTest |
| Single-node integration tests | LocalTest |
| Multi-node cluster integration tests | Clustered |
| Local development | LocalTest (fast) or Clustered (parity) |
| Production | Clustered |

#### Relation to AkkaTeach

This repo runs a **single local** `ActorSystem` with `EventStream` and plain parent/child actors — no sharding, no `AkkaExecutionMode` switch. That keeps the teaching surface small. When you add per-entity actors (one actor per order, user, session ID) and later deploy to a cluster, adopt `AkkaExecutionMode` + `GenericChildPerEntityParent` + `IPubSubMediator` so tests stay fast without forking application logic.

Further reading in the skill repo:

- [cluster-local-abstractions.md](https://github.com/aaronontheweb/dotnet-skills/blob/master/skills/akka-best-practices/cluster-local-abstractions.md) — full `GenericChildPerEntityParent` and mediator code
- [akka-hosting-actor-patterns](https://github.com/aaronontheweb/dotnet-skills/tree/master/skills/akka-hosting-actor-patterns) — entity actors, message extractors, reminders
- [work-distribution-patterns.md](https://github.com/aaronontheweb/dotnet-skills/blob/master/skills/akka-best-practices/work-distribution-patterns.md) — DB queues, Akka.Streams, outbox

---

## Related docs

- [README](../README.md) — how to run the worker and tests
- [Akka.NET documentation](https://getakka.net/)
- [akka-net-best-practices skill](https://github.com/aaronontheweb/dotnet-skills/tree/master/skills/akka-best-practices) — source for this section
