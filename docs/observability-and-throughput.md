# Throughput and OpenTelemetry (AkkaTeach)

Reference numbers for **message throughput** and **observability** in Akka.NET. Lab benchmarks and production reality differ — read the caveats first.

---

## Message throughput — what to expect

Akka.NET throughput is usually **not** the bottleneck in business applications. Handler logic, serialization, database calls, and HTTP dominate. Benchmarks below measure **empty or minimal handlers** (often ping-pong) to show transport/mailbox ceilings.

### Single actor, in-process (mailbox only)

| Scenario | Approx. throughput | Source |
|----------|-------------------|--------|
| Minimal handler, no APM | **~7–8 million msg/s** per actor | [Phobos performance benchmarks](https://phobos.petabridge.com/articles/performance.html) (.NET 9, single actor, NBench) |
| Phobos monitoring only | **~6.3 million msg/s** (~20% reduction) | Same |
| Phobos full trace + metrics | **~3.7 million msg/s** (~53% reduction) | Same |

Petabridge quotes **7–8 million messages per second** for a single actor on modern hardware when the handler does almost no work ([Akka.NET platform page](https://petabridge.com/platform/akka-net/)).

**Reality check:** a web app at 1,000 req/s × 10 actor messages ≈ **10,000 msg/s** — well under 1% of even instrumented capacity. Phobos docs note most production apps never approach these limits.

### Akka.Remote (cross-process, small messages)

Official [RemotePingPong](https://getakka.net/articles/remoting/performance.html) numbers (12-core laptop, .NET Core 2.1 era):

| Configuration | Typical range |
|---------------|---------------|
| Default DotNetty | **~70K–190K msg/s** (varies with concurrent client actors) |
| With I/O batching enabled | **~100K–190K msg/s**, lower variance |

Modern dev hardware (e.g. AMD Ryzen 9 9900X, Akka.NET 1.5.x benchmarks) reports **~1.3–1.5 million msg/s** remote ping-pong over DotNetty, with experimental Artery.Tcp optimizations reaching **~1.8M msg/s** in controlled tests ([akka.net PR #8203](https://github.com/akkadotnet/akka.net/pull/8203), [commit c8e107a](https://github.com/akkadotnet/akka.net/commit/c8e107a0cf1960f81f98a33b6c9fb63a97be2c29)).

Treat remote numbers as **order-of-magnitude guides** — payload size, serialization version, CPU, and batching dominate.

### What kills throughput in real apps

| Anti-pattern | Effect |
|--------------|--------|
| Blocking in `Receive` (`.Result`, `Thread.Sleep`) | Serializes mailbox; throughput collapses |
| Large message payloads | Serialization + GC cost |
| `Ask` storms | Thread pool pressure |
| One giant actor for all work | Single mailbox bottleneck — use routers / many actors |
| Tracing every message type with no filter | Memory/GC from string formatting (Phobos) |

AkkaTeach patterns that preserve throughput: `PipeTo`, routers (`ScatterGatherCoordinatorActor`, `DataIngestionActor`), fan-out with known reply counts.

---

## OpenTelemetry in Akka.NET

OpenTelemetry (OTel) is the standard .NET observability pipeline (traces, metrics, logs). Akka.NET support is **layered**:

| Layer | What it covers | Cost |
|-------|----------------|------|
| **Akka.Streams** (built-in, free) | Stream pipeline stage spans | Zero overhead until `AddSource("Akka.Streams")` registered |
| **Log ↔ trace correlation** (Akka.Hosting) | Actor logs linked to parent trace/span | Opt-in processor |
| **Phobos** (Petabridge) | Actors, cluster, remoting, persistence, sharding | Commercial; primary full actor APM |

There is **no** built-in automatic instrumentation of every actor mailbox in core Akka.NET — that is what **Phobos** provides.

### 1. Akka.Streams tracing (free, since Akka.NET 1.5.66)

Opt-in. Register the streams `ActivitySource`:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Akka.Streams")   // or StreamsDiagnostics.ActivitySourceName
        .AddOtlpExporter());

// No listener registered → zero allocations, zero overhead
```

Each stream stage emits spans parented to the producer trace — including fan-in/fan-out and async boundaries. See [Stream Tracing docs](https://getakka.net/articles/streams/stream-tracing.html).

Works **without Phobos**. Phobos adds actor-to-stream continuity when both are enabled.

### 2. Actor log ↔ trace correlation (Akka.Hosting)

`Activity.Current` does not flow across mailbox boundaries. Akka.NET captures `ActivityContext` at log time so OTLP logs can carry trace/span IDs.

```csharp
builder.Logging.AddOpenTelemetry(options =>
{
    options.AddAkkaTraceCorrelation();  // Akka.Hosting extension
    options.AddOtlpExporter();
});
```

See [Akka.Hosting issue #700](https://github.com/akkadotnet/Akka.Hosting/issues/700) and the [log-trace-correlation POC](https://github.com/Aaronontheweb/akka.net-log-trace-correlation-POC).

Route actor logging through `Microsoft.Extensions.Logging` / `LoggerFactoryLogger` for native OTLP correlation.

### 3. Phobos — OpenTelemetry for actors and cluster (commercial)

[Phobos](https://phobos.petabridge.com/) injects OTel instrumentation at runtime — **no `[Receive]` wrapper code**.

**Akka.Hosting setup:**

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddPhobosInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddPhobosInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.AddAkka("AkkaTeach", (akkaBuilder, sp) =>
{
    akkaBuilder
        .WithRemoting("127.0.0.1", 4053)
        .WithClustering(new ClusterOptions { /* ... */ })
        .WithPhobos(AkkaRunMode.Local);  // or AkkaRunMode.AkkaCluster
});
```

**Typical metrics (Phobos → OTel):**

- Message throughput by actor type / message type
- Mailbox backlog / processing latency (P50/P95/P99)
- Cluster node status, sharding entity distribution
- Actor start/stop, crash rates

**Typical trace spans:**

- `akka.msg.recv {MsgType}` — message received
- `akka.actor.ask {MsgType}` — Ask end-to-end
- Cluster/remoting propagation across nodes

**Noise control (essential):** actor messaging is cheap, so tracing everything floods backends. Use:

- `ITraceFilter` — trace only business message types
- OTel sampling — `TraceIdRatioBasedSampler`
- OTel metric views — drop high-cardinality series (e.g. `akka.messages.latency*`)

See [Phobos trace filtering](https://phobos.petabridge.com/articles/trace-filtering.html) and [quickstart](https://phobos.petabridge.com/articles/quickstart.html).

### Choosing an observability stack

| Need | Recommendation |
|------|----------------|
| Stream pipeline debugging | `AddSource("Akka.Streams")` only |
| Correlate actor logs with HTTP traces | `AddAkkaTraceCorrelation()` + Serilog/MEL |
| Production actor/cluster APM | Phobos + OTLP → Grafana / Jaeger / Datadog |
| AkkaTeach local dev | Serilog console (already in `AkkaTeach.Worker`) — no OTel required |

---

## Phobos — limitations and when it is overkill

Phobos 2.x is **OpenTelemetry-native** and much easier to wire (`WithPhobos` + `AddPhobosInstrumentation`) than Phobos 1.x. It is still **commercial APM for actor/cluster systems** — not a default dependency for every Akka.NET app.

### Licensing and access limits

| Constraint | Detail |
|------------|--------|
| **Price** | ~**$4,000/year per organization** — unlimited nodes and users within that org ([pricing](https://phobos.petabridge.com/articles/buy.html)) |
| **No free trial** | 30-day money-back guarantee instead |
| **Distribution** | Private Sdkbin NuGet feed — license key required; not on nuget.org |
| **Legal entities** | Separate companies/customers need separate licenses |
| **Production SLA** | Optional support plans ($4k–$10k/year) are **separate** from the Phobos license |

### Technical and operational limits

| Limit | Detail |
|-------|--------|
| **Not open source** | Tied to Petabridge releases and licensing |
| **Automatic tracing is noisy** | Actor messaging is cheap → huge trace volume without `ITraceFilter`, OTel sampling, and metric views |
| **Backend costs are yours** | Phobos exports OTel; Grafana Cloud, Datadog, storage, etc. are extra |
| **Tracing overhead** | Full instrumentation ~53% throughput drop on **micro-benchmarks** (~7.9M → ~3.7M msg/s per actor); irrelevant for most apps, painful if you trace every high-frequency message |
| **Domain semantics** | Sees actor paths and message **types**, not business meaning — unless you filter/design for it |
| **OTel pipeline still required** | Exporters, collectors, dashboards — Phobos is instrumentation only |

Petabridge targets Phobos at **highly available, large-scale Akka.Cluster** deployments — not single-node demos like AkkaTeach.

### What changed recently (Phobos 2.x + Akka.NET 1.5.x)

| Before | Now |
|--------|-----|
| Per-node Phobos pricing | **Flat org license** (~$4k/year) |
| Vendor-specific APM adapters | **OpenTelemetry** — any OTLP backend |
| HOCON-heavy setup | **`WithPhobos(AkkaRunMode.*)`** via Akka.Hosting |
| Phobos needed for stream visibility | **Akka.Streams OTel built-in free** (1.5.66+) — `AddSource("Akka.Streams")` |
| Awkward log/trace linking | **`AddAkkaTraceCorrelation()`** on MEL + OTel logging |
| Logs appended to spans by default | **Phobos 2.11+**: `AppendLogsToTrace` deprecated — use structured logs + correlation |

**Implication:** you no longer need Phobos for **streams** or **log/trace correlation**. Phobos’s remaining sweet spot is **zero-code actor + cluster + sharding APM** with cross-node trace propagation.

### When Phobos is overkill

- Single local `ActorSystem` — Serilog + TestKit + event stream suffice (AkkaTeach)
- Few actors, **no cluster** — manual `ActivitySource` at HTTP/facade boundaries is often enough
- **Akka.Streams–heavy**, not actor-heavy — free stream tracing only
- You only need liveness — Akka.Hosting health checks + app metrics
- Low message volume, small team — a handful of custom spans beats license + filter tuning

**Community alternative:** [Akka.OpenTelemetry](https://github.com/asynkron/Akka.OpenTelemetry) (Apache-licensed) — WIP; **no cluster support**; traces all `/user` actors with limited configuration. Not a production cluster replacement for Phobos.

### When Phobos is worth it

- **Akka.Cluster + sharding** in production — mailbox depth, shard distribution, node reachability
- Debugging **cross-node message flows** you cannot reproduce locally
- No team bandwidth to maintain custom actor instrumentation + remote trace-context serializers
- Incidents where you need “which actor on which node handled this message?” quickly

### Decision guide

```
Local dev / teaching        → Serilog + TestKit (+ Akka.Streams OTel if using streams)
Single-node production      → OTel for ASP.NET/HTTP/EF
                              + AddAkkaTraceCorrelation()
                              + targeted manual spans at boundaries
Cluster production          → Phobos (or heavy custom OTel investment)
                              + ITraceFilter + OTel sampling
                              + pre-built Grafana dashboards (included with Phobos)
```

**Rule of thumb:** if you are not running **cluster + remoting** and do not regularly debug **distributed actor message flows in production**, Phobos is probably overkill. Buy it when cluster observability becomes a recurring production cost center.

---

## Related

- [actor-model-guide.md §17](actor-model-guide.md#17-throughput-and-opentelemetry) — summary in the main guide
- [cluster-hosting-configuration.md](cluster-hosting-configuration.md) — programmatic cluster setup
- [Phobos performance impact](https://phobos.petabridge.com/articles/performance.html)
- [Akka.NET remoting performance](https://getakka.net/articles/remoting/performance.html)
