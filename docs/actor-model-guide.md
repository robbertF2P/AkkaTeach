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

## 10. Stash — buffer messages until ready

When an actor **cannot handle certain messages yet** (dependencies loading, `PipeTo` in flight, waiting for refs), **`Stash.Stash()`** stores the current message in memory. When behavior switches to ready, **`Stash.UnstashAll()`** prepends stashed messages to the mailbox **in original order**.

**Files:**

- `src/AkkaTeach.Core/Actors/StashGateActor.cs`
- `src/AkkaTeach.Contracts/StashMessages.cs`

```text
Client ──ProcessItem──► Gate (Waiting)
                           │
                           │ Stash.Stash() × N
                           ▼
                     [in-memory stash]
                           │
              timer / deps ready
                           ▼
                     Become(Ready)
                     UnstashAll()
                           │
                           ▼
Client ◄── replies ── ProcessItem handlers (FIFO)
```

**Implement `IWithUnboundedStash`:**

```csharp
public sealed class StashGateActor : ReceiveActor, IWithUnboundedStash, IWithTimers
{
    public IStash Stash { get; set; } = null!;

    Receive<ProcessGatedItemCommand>(_ => Stash.Stash());

    // when dependencies are ready:
    Become(Ready);
    Stash.UnstashAll();
}
```

Akka populates `Stash` after construction and switches the actor to a **deque mailbox** automatically.

### When to stash

| Situation | Stash? |
|-----------|--------|
| Messages arrive before init completes | Yes |
| Wrong behavior state (`Become` not ready yet) | Yes — or ignore/log |
| Message should be dropped | No — do not stash |
| Long-term storage | No — use persistence or a database |

### Stash vs ignore

`SessionActor` (§8) **ignores** wrong-state messages. Stash **preserves order** for later — use when early messages are valid but untimely.

### Rules

- Never stash the **same message twice** — throws `IllegalActorStateException`
- `UnstashAll()` prepends to mailbox; order preserved (oldest first)
- On actor restart, stash is cleared; `PreRestart` on stash actors typically calls `UnstashAll()` — design for idempotency
- Prefer **`IWithUnboundedStash`** unless you explicitly need a bounded stash capacity

**Tests:** `tests/AkkaTeach.Tests/Actors/StashGateActorTests.cs`

---

## 11. Scheduler — delay and repeat messages

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

## 12. Routers / worker pools

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

## 13. Event stream — pub/sub

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

## 14. Keep I/O outside actors

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
| Scheduler / timers | `IWithTimers`, `ScheduleTellOnce` | See [§11](#11-scheduler--delay-and-repeat-messages) |
| PipeTo in a real flow | `DataIngestionActor` | `DataIngestionActorTests` |
| Worker pools | `DataIngestionActor` | `DataIngestionActorTests` |
| Event stream | `SessionActor`, `PeerActor` | `SessionActorTests`, `PeerActorTests` |
| Akka.Hosting / DI | `AkkaHostingExtensions` | `AkkaHostingRegistrationTests` |
| Clustering / remoting / HA | `ClusterOptions`, §15 | See [§15](#15-clustering-remoting-and-high-availability) |
| Kamikaze / error kernel | `KamikazeManagerActor` | `KamikazeActorTests` |
| Scatter-gather | `ScatterGatherCoordinatorActor` | `ScatterGatherCoordinatorActorTests` |
| Throughput / OpenTelemetry | See §18 | [observability-and-throughput.md](observability-and-throughput.md) |
| Stash | `StashGateActor` | `StashGateActorTests` |
| Saga / compensation | `OrderSagaActor` | `OrderSagaActorTests` |

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
| Use `IWithTimers` or `ScheduleTellOnce` for delays/retries | Never `Thread.Sleep` in handlers | See [§11](#11-scheduler--delay-and-repeat-messages); P6 retry backoff |
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

## 15. Clustering, remoting, and high availability

Sections 1–13 run in a **single local** `ActorSystem`. Production systems that need fault tolerance across machines use **Akka.Remote** (transport) and **Akka.Cluster** (membership, routing, singleton, sharding).

**Configuration:** use **Akka.Hosting** programmatic APIs (`WithRemoting`, `WithClustering`, `ClusterOptions`) — not hand-written HOCON. See [cluster-hosting-configuration.md](cluster-hosting-configuration.md).

### Akka.Remote — talk across machines

**Remoting** adds a TCP transport so one `ActorSystem` can `Tell` actors on another process.

```
Node A (AkkaTeach)                    Node B (AkkaTeach)
┌─────────────────────┐              ┌─────────────────────┐
│ /user/order-123     │──Tell──────► │ /user/inventory     │
│ ActorSystem: AkkaTeach              │ ActorSystem: AkkaTeach
│ port 4053                           │ port 4054
└─────────────────────┘              └─────────────────────┘
```

Programmatic setup (Akka.Hosting 1.5.x):

```csharp
builder.Services.AddAkka("AkkaTeach", configurationBuilder =>
{
    configurationBuilder
        .WithRemoting(hostname: "127.0.0.1", port: 4053)
        .WithClustering(new ClusterOptions
        {
            Roles = ["worker"],
            SeedNodes = [Address.Parse("akka.tcp://AkkaTeach@127.0.0.1:4053")],
            SplitBrainResolver = SplitBrainResolverOption.Default,
        });
});
```

| API | Purpose |
|-----|---------|
| `WithRemoting(hostname, port)` | Bind address + public remoting port |
| `WithClustering(ClusterOptions)` | Join cluster, roles, seeds, SBR |
| `ClusterOptions.SeedNodes` | Static join targets (Lighthouse addresses) |
| `ClusterOptions.SplitBrainResolver` | Partition strategy (`KeepMajorityOption`, etc.) |

**Rule:** remoting alone does not give elastic scale-out or automatic failover. Prefer **Akka.Cluster** for production HA.

### Akka.Cluster — peer-to-peer membership

A **cluster** is a set of `ActorSystem` instances that share the same **system name** and discover each other via **seed nodes** (or Akka.Management bootstrap).

```
                    ┌──────────────┐
                    │  Lighthouse  │  seed nodes (stable addresses)
                    │  0, 1, 2     │
                    └──────┬───────┘
           join            │            join
     ┌──────────┐    ┌─────┴─────┐    ┌──────────┐
     │ Worker 1 │◄──►│  Gossip   │◄──►│ Worker 2 │
     │ role:    │    │  protocol │    │ role:    │
     │ worker   │    └───────────┘    │ worker   │
     └──────────┘                     └──────────┘
```

| Term | Role |
|------|------|
| **Node** | One running `ActorSystem` instance |
| **Member** | A node that has joined the cluster |
| **Seed node** | Well-known join address (often Lighthouse) |
| **Leader** | Deterministically chosen member that applies membership changes |
| **Role** | Tag (e.g. `worker`, `web`) for workload placement |
| **Failure detector** | Heartbeat-based; marks peers `UNREACHABLE` |

### Gossip protocol — how the cluster learns who is alive

Akka.Cluster does **not** use a central coordinator. Each node maintains a local view of membership and **gossips** that view to random peers until all nodes converge.

Typical flow when node C joins:

1. C contacts a **seed node** and sends a join request.
2. The **leader** (oldest reachable node) adds C to the membership ring.
3. Every node periodically gossips its membership table to a few random peers.
4. When node B crashes, the **failure detector** on other nodes marks B `UNREACHABLE`.
5. After `SplitBrainResolverStableAfter` (configured on `ClusterOptions`) the leader **downs** B.
6. Gossip propagates the new membership; cluster-aware routers and singleton managers react.

```
Time ─────────────────────────────────────────────────────────►

Node A gossip:  [A*, B, C]  ──►  [A*, B↓, C]  ──►  [A*, C]
Node B:         (crashed)
Node C gossip:  [A, B, C*]  ──►  [A, B↓, C*]  ──►  [A, C*]

* = this node's view; ↓ = downed
```

**Why it matters:** routers, cluster singleton, and sharding all subscribe to membership events. When gossip says a node left, work migrates elsewhere — that is the HA story.

### Lighthouse — dedicated seed nodes

[Lighthouse](https://github.com/petabridge/lighthouse) is a small Petabridge service whose only job is to stay up as a **seed node** so application pods can find the cluster.

Benefits:

- Application nodes can restart without becoming seeds themselves.
- Stable DNS names in Kubernetes (`lighthouse-0.lighthouse`, …).
- Ships with **Petabridge.Cmd** on port 9110 for cluster inspection.

Your app nodes still configure joins **programmatically**:

```csharp
.WithClustering(new ClusterOptions
{
    Roles = ["worker"],
    SeedNodes =
    [
        Address.Parse("akka.tcp://AkkaTeach@lighthouse-0.lighthouse:4053"),
        Address.Parse("akka.tcp://AkkaTeach@lighthouse-1.lighthouse:4053"),
    ],
    SplitBrainResolver = SplitBrainResolverOption.Default,
});
```

Lighthouse Docker (seed process only):

```bash
docker run --name lighthouse1 --hostname lighthouse1 \
  -p 4053:4053 -p 9110:9110 \
  --env ACTORSYSTEM=AkkaTeach \
  --env CLUSTER_IP=lighthouse1 \
  --env CLUSTER_PORT=4053 \
  --env CLUSTER_SEEDS="akka.tcp://AkkaTeach@lighthouse1:4053" \
  petabridge/lighthouse:latest
```

For Kubernetes without static seeds, prefer **Akka.Management** + discovery — see [cluster-hosting-configuration.md](cluster-hosting-configuration.md).

### High availability via Akka.Hosting

| Feature | Hosting API | HA behavior |
|---------|-------------|-------------|
| Cluster singleton | `.WithSingleton<T>(name, props, options)` | One instance cluster-wide; fails over to oldest node |
| Singleton proxy | `.WithSingletonProxy<T>(...)` | Stable ref while instance moves |
| Cluster sharding | `.WithShardRegion<T>(...)` | Entities rebalance by shard key |
| Distributed pub/sub | `.WithDistributedPubSub(role)` | Cross-node pub/sub (not local `EventStream`) |
| Split Brain Resolver | `ClusterOptions.SplitBrainResolver` | Partition safety for singleton/sharding |

Example — cluster singleton + proxy:

```csharp
configurationBuilder
    .WithRemoting("0.0.0.0", 4053)
    .WithClustering(new ClusterOptions
    {
        Roles = ["worker"],
        SeedNodes = lighthouseSeeds,
        SplitBrainResolver = new KeepMajorityOption(),
    })
    .WithSingleton<JobScheduler>(
        singletonName: "job-scheduler",
        actorProps: resolver.Props<JobScheduler>(),
        options: new ClusterSingletonOptions { Role = "worker" },
        createProxyToo: true);
```

#### Failover timeline (defaults, approximate)

When the node hosting a cluster singleton crashes:

```
0s     Failure detector marks node UNREACHABLE (missed heartbeats)
20s    SBR stable-after elapses — partition strategy runs
20s    Leader downs failed member
40s    down-removal-margin — old singleton must be gone
40s+   New singleton starts on oldest surviving node
```

Tune via `ClusterOptions.DownRemovalMargin`, `ClusterOptions.SplitBrainResolverStableAfter`, and `ClusterSingletonOptions` — not raw HOCON unless you have no Hosting API equivalent.

#### Split brain — why HA needs an explicit strategy

A **network partition** can split the cluster into islands that each think they are valid. Without SBR, each island might spin up its own singleton → **duplicate writers**.

```csharp
SplitBrainResolver = new KeepMajorityOption { Role = "worker" }
// or: new KeepOldestOption { ... }
// or: SplitBrainResolverOption.Default
```

Always configure SBR in production when using singleton or sharding. See [Split Brain Resolver docs](https://getakka.net/articles/clustering/split-brain-resolver.html).

#### HA platform checklist

1. **Lighthouse** or **Akka.Management** + discovery for cluster formation.
2. **Same `ActorSystem` name** on every node (`AddAkka("AkkaTeach", ...)`).
3. **`WithRemoting` + `WithClustering(ClusterOptions)`** — programmatic config.
4. **Roles** to separate web frontends from worker nodes.
5. **`.WithSingleton` / `.WithShardRegion`** — not raw remoting paths.
6. **`ClusterOptions.SplitBrainResolver`** for partition safety.
7. **Akka.Persistence** when state must survive restarts and node moves.

AkkaTeach does not ship a multi-node cluster demo (that needs Lighthouse + several processes). Use [cluster-hosting-configuration.md](cluster-hosting-configuration.md) and Petabridge's [Akka.Cluster bootcamp](https://petabridge.com/cluster/) for hands-on cluster labs.

---

## 16. Kamikaze actors — isolate risky work

The **kamikaze pattern** (sometimes called the **error kernel** in a micro form) pushes dangerous or one-shot work into a **short-lived child actor**. The parent:

1. Spawns the child.
2. **`Context.Watch`** the child.
3. Waits for either a **success message** or **`Terminated`** (crash / stop without result).

The child performs the work, reports back, and **`Context.Stop(Self)`**. If it throws, supervision stops it; the parent's death watch turns that into a failure reply.

**Files:**

- `src/AkkaTeach.Core/Actors/KamikazeManagerActor.cs` — long-lived coordinator
- `src/AkkaTeach.Core/Actors/KamikazeWorkerActor.cs` — short-lived executor
- `src/AkkaTeach.Contracts/KamikazeMessages.cs` — commands and replies

```
Client ──RunKamikazeTask──► KamikazeManager
                                  │
                                  │ ActorOf + Watch
                                  ▼
                             KamikazeWorker
                                  │
                     success ─────┤───── crash
                                  │
                     Tell result  │  Terminated
                     Stop self    │
                                  ▼
Client ◄── KamikazeTaskSucceeded / Failed ── KamikazeManager
```

**Manager — spawn and watch:**

```csharp
var worker = Context.ActorOf(
    KamikazeWorkerActor.Props(command.TaskId, command.ShouldFail),
    $"kamikaze-{command.TaskId}");
Context.Watch(worker);
```

**Worker — execute once, report, self-destruct:**

```csharp
Context.Parent.Tell(new KamikazeWorkCompleted(_taskId, $"Result for {_taskId}"));
Context.Stop(Self);
```

**Manager — death watch catches crashes:**

```csharp
Receive<Terminated>(_ =>
{
    if (!_resultReceived && _pendingTaskId is not null)
    {
        _pendingRequester?.Tell(new KamikazeTaskFailed(_pendingTaskId, "Worker terminated before reporting success"));
    }
});
```

### When to use kamikaze vs PipeTo

| Situation | Pattern |
|-----------|---------|
| Async I/O that returns a `Task` | `PipeTo(Self, ...)` — see §9 |
| Risky code that may throw; must not kill the parent | Kamikaze child + death watch |
| Stateless retry with supervision | Kamikaze manager with `OneForOneStrategy` + bounded retries |
| Long-lived state | Keep in the **parent**; never in the kamikaze child |

### Relation to clustering

On a cluster, the same pattern applies per node: a sharded entity actor can spawn kamikaze children for isolated retries without corrupting entity state. When the node goes down, sharding + gossip move the **entity** to another node — kamikaze children are ephemeral and die with the node.

**Tests:** `tests/AkkaTeach.Tests/Actors/KamikazeActorTests.cs`

---

## 17. Scatter-gather — fan-out and aggregate

When you know **how many replies to expect**, fan work out to workers and aggregate results in a coordinator. This is one of the most common multi-actor patterns (parallel price lookups, multi-source validation, batch enrichment).

**File:** `src/AkkaTeach.Core/Actors/ScatterGatherCoordinatorActor.cs`

Contrast with `WorkCoordinatorActor` (§7): that actor waits for **one** child reply. Scatter-gather waits for **N** replies before replying once.

```
Client ──ScatterGatherCommand──► Coordinator
                                     │
                         Tell(item, Self) × N
                                     ▼
                              Worker pool (router)
                                     │
                         WorkItemProcessed × N
                                     ▼
Client ◄── ScatterGatherCompleted ── Coordinator
           (all results)
```

**Start batch — fan out with `Self` as sender:**

```csharp
_expectedCount = command.Items.Count;
_receivedCount = 0;
_results.Clear();

foreach (var item in command.Items)
{
    _workerPool.Tell(item, Self);
}

Become(Aggregating);
```

**Aggregate — count until complete:**

```csharp
Receive<WorkItemProcessed>(result =>
{
    _results.Add(result);
    _receivedCount++;

    if (_receivedCount < _expectedCount)
    {
        return;
    }

    _requester?.Tell(new ScatterGatherCompleted(_batchId, _results.ToList()));
    Become(Idle);
});
```

While aggregating, `ScatterGatherStatusQuery` returns `"Aggregating"` with `{Received}/{Expected}` — proof the mailbox stays open for partial replies.

### Design notes

| Practice | Why |
|----------|-----|
| Pass `Self` as sender on fan-out | Coordinator receives all worker replies |
| Store `_expectedCount` from command | You must know N up front |
| Use `Become(Aggregating)` | Ignore or reject new batches until current one completes |
| Router pool for workers | Parallel throughput without manual thread management |
| Reply once at the end | Caller gets a single aggregated message |

### Variations

| Need | Adjustment |
|------|------------|
| Timeout if a worker never replies | `IWithTimers` + fail batch after deadline |
| Partial results OK | Reply when `_receivedCount >= minimumRequired` |
| Unknown worker count | Different pattern (register interest, first reply wins, etc.) |
| Cluster-wide fan-out | Cluster-aware router or tell shard regions |

**Tests:** `tests/AkkaTeach.Tests/Actors/ScatterGatherCoordinatorActorTests.cs`

---

## 18. Throughput and OpenTelemetry

Full reference: [observability-and-throughput.md](observability-and-throughput.md).

### Message throughput (lab vs production)

Benchmarks use **minimal handlers** (ping-pong). Your business logic usually dominates.

| Layer | Order of magnitude | Notes |
|-------|-------------------|--------|
| Single actor, in-process | **~7–8M msg/s** | Empty handler; Phobos/NBench on .NET 9 |
| Same actor + Phobos full OTel | **~3.7M msg/s** | ~53% reduction; still huge headroom |
| Akka.Remote (small msgs) | **~100K–1.5M msg/s** | Old laptop docs ~100–190K; modern HW ~1.4M+ |
| Typical web + actors | **~10K msg/s** | Example: 1K req/s × 10 tells |

**Takeaway:** design for correctness first (`PipeTo`, routers, scatter-gather). Measure your app — do not size from ping-pong alone.

Blocking in `Receive`, large payloads, and unfiltered tracing hurt throughput more than the mailbox itself.

### OpenTelemetry support

| Component | Built-in? | Setup |
|-----------|-----------|--------|
| **Akka.Streams** spans | Yes (1.5.66+) | `.AddSource("Akka.Streams")` on OTel tracing |
| **Actor log ↔ trace** | Akka.Hosting | `.AddAkkaTraceCorrelation()` on logging OTel |
| **Actor/cluster APM** | **Phobos** (commercial) | `.WithPhobos(...)` + `.AddPhobosInstrumentation()` |

Core Akka.NET does **not** auto-trace every mailbox. Phobos fills that gap and exports to any OTLP backend (Grafana, Jaeger, Datadog, Seq).

**Minimal Phobos + OTel wiring:**

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddPhobosInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddPhobosInstrumentation().AddOtlpExporter());

services.AddAkka("AkkaTeach", b => b.WithPhobos(AkkaRunMode.Local));
```

Use `ITraceFilter` and OTel sampling — actor systems generate enormous trace volume if you record every message.

AkkaTeach uses **Serilog** locally (`Program.cs`); add OTel when you deploy clustered actors to production.

**Phobos buy vs build:** see [observability-and-throughput.md — Phobos limitations](observability-and-throughput.md#phobos--limitations-and-when-it-is-overkill).

---

## 19. Saga — multi-step workflow with compensation

A **saga** (process manager) coordinates a **long-running business transaction** across multiple steps. Each step uses `Become` to wait for the next reply. If a later step fails, run **compensating actions** in reverse order for work already done.

**File:** `src/AkkaTeach.Core/Actors/OrderSagaActor.cs`

Forward flow:

```
ReserveInventory ──► ChargePayment ──► ShipOrder ──► Completed
```

Failure at ship (after charge + reserve):

```
Ship fails ──► RefundPayment ──► ReleaseInventory ──► OrderSagaFailed
```

**Start saga:**

```csharp
Receive<StartOrderSagaCommand>(command =>
{
    _requester = Sender;
    Self.Tell(new SagaReserveInventory(command.OrderId));
    Become(Reserving);
});
```

**Forward step with `Become`:**

```csharp
Receive<SagaInventoryReserved>(reserved =>
{
    _inventoryReserved = true;
    Self.Tell(new SagaChargePayment(reserved.OrderId, _amount));
    Become(Charging);
});
```

**Compensate in reverse on failure:**

```csharp
if (_paymentCharged)
{
    Become(Compensating);
    Self.Tell(new SagaRefundPayment(_orderId, _amount));
    return;
}

if (_inventoryReserved)
{
    Become(Compensating);
    Self.Tell(new SagaReleaseInventory(_orderId));
}
```

### Saga vs scatter-gather (§17)

| Pattern | Purpose |
|---------|---------|
| **Scatter-gather** | Parallel fan-out; aggregate N replies of the **same shape** |
| **Saga** | Sequential steps; **different** operations; failure triggers rollback |

### Saga vs session FSM (§8)

| Pattern | Purpose |
|---------|---------|
| **Session FSM** | One entity's lifecycle states |
| **Saga** | Cross-service workflow with compensations |

Production sagas often combine **stash** (commands arrive before saga ready), **PipeTo** (async IO per step), and **timers** (step timeouts).

**Tests:** `tests/AkkaTeach.Tests/Actors/OrderSagaActorTests.cs`

---

## Related docs

- [README](../README.md) — how to run the worker and tests
- [Akka.NET documentation](https://getakka.net/)
- [akka-net-best-practices skill](https://github.com/aaronontheweb/dotnet-skills/tree/master/skills/akka-best-practices) — source for this section
