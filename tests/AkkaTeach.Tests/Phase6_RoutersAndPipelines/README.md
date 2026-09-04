# Phase 6 — Routers and pipelines

**Question this phase answers:** how do I parallelise work and keep IO out of my actors?

This phase combines everything so far into one realistic pipeline: fetch pages from an API,
fan the records out across a worker pool, and aggregate the results.

## The pipeline

```
   caller
     │ CollectDataCommand
     ▼
   ┌──────────────────────┐        ┌─────────────────┐
   │  DataIngestionActor  │──Ask──►│  IDataApiClient │ (injected, mockable)
   │                      │◄─Pipe──│                 │
   │  Become(Collecting)  │        └─────────────────┘
   │  counts + aggregates │
   └──────────┬───────────┘
              │ one message per record
              ▼
      RoundRobinPool router
       ┌──────┼──────┬──────┐
       ▼      ▼      ▼      ▼
     w1     w2     w3     w4        ← DataRecordWorkerActor
       │      │      │      │
       └──────┴───┬──┴──────┘
                  │ DataRecordProcessed
                  ▼
          back to DataIngestionActor
                  │ all done?
                  └──publish──► EventStream
```

## Routers

A router is an actor that forwards to a pool of identical children. `RoundRobinPool(4)` gives you
four workers and cycles between them.

```
   Context.ActorOf(
       DataRecordWorkerActor.Props().WithRouter(new RoundRobinPool(4)),
       "workers");
```

- The router is a normal `IActorRef` — the sender does not know it is talking to a pool.
- Scale the number, not the code.
- Other strategies exist (broadcast, smallest-mailbox, consistent-hash) for other shapes of work.

## The IO boundary

The actor depends on an **interface**, injected through `Props`. That is what makes it testable
without a network.

```
   DataIngestionActor ──► IDataApiClient
                             ├── MockDataApiClient  (tests, demo)
                             └── HttpDataApiClient   (real life)
```

> Actors should never `new` up an `HttpClient` or reach for `IServiceProvider`. Resolve
> dependencies when you build the `Props`.

## What is reused here

| From | Used for |
|---|---|
| Phase 3 | `Sender`, EventStream publishing |
| Phase 4 | `Become(Collecting)` while a run is in progress |
| Phase 5 | `PipeTo` for every page fetch |

## Tests here

- `DataIngestionActorTests` — full run over all pages, every record processed, lifecycle events
- `MockDataApiClientTests` — the fake client itself behaves as specified

---

[← Phase 5 — Async work](../Phase5_AsyncWork/README.md)  |  [Index](../README.md)  |  [Phase 7 — Hosting →](../Phase7_Hosting/README.md)
