# Phase 7 — Hosting

**Question this phase answers:** how does an actor system live inside a real .NET application?

## Wiring

```
   Program.cs (the only composition root)
        │
        │ services.AddAkka("AkkaTeach", builder => …)
        ▼
   ┌──────────────────────────────────────────────────┐
   │  .NET Generic Host                               │
   │                                                  │
   │   DI container ──────► ActorSystem (singleton)   │
   │        │                     │                   │
   │        │ IDataApiClient      │ WithActors        │
   │        │ IOptions<T>         ▼                   │
   │        │              ┌─────────────────┐        │
   │        └─────────────►│ work-coordinator│        │
   │                       │ session         │        │
   │                       │ data-ingestion  │        │
   │                       └────────┬────────┘        │
   │                                │ Register<T>     │
   │                                ▼                 │
   │                          ActorRegistry           │
   └──────────────────────────────────────────────────┘
                                    │
             IRequiredActor<T> ─────┘
                     │
                     ▼
        Console REPL / worker / controller
```

## Rules

- **One `ActorSystem` per application**, owned by the host. Never one per request.
- Register actors once, in `AkkaHostingExtensions.AddAkkaTeachActors` — both the Worker and the
  Console reuse that single method.
- Give top-level actors **stable names** (`work-coordinator`, `session`, `data-ingestion`).
  They appear in paths and logs.
- Resolve dependencies at `Props` creation time and pass them in — do not inject `IServiceProvider`
  into an actor.
- Non-actor code reaches actors through `IRequiredActor<T>` or a facade, never by path lookup.

## Crossing the boundary

```
   non-actor world          │          actor world
   (controller, REPL)       │
        │                   │
        └── Ask<T>(msg, timeout) ──► actor
                            │           │
                            │◄──────────┘ Sender.Tell(reply)
```

`Ask` is the edge adapter: it turns a message exchange into a `Task`. Inside the actor world,
keep using `Tell`.

## Tests here

`AkkaHostingRegistrationTests` — the registered actors are resolvable from the `ActorRegistry`,
proving the host wiring is correct.

---

[← Phase 6 — Routers and pipelines](../Phase6_RoutersAndPipelines/README.md)  |  [Index](../README.md)  |  [Course index →](../README.md)
