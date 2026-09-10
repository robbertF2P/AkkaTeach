# Phase 5 — Waiting without blocking

**Question this phase answers:** the mailbox handles one message at a time — so how do I wait
for slow IO or dependencies without freezing the actor or dropping useful work?

## The problem

```
   ❌ blocking inside a handler

   mailbox: [ Fetch ][ B ][ C ][ D ]
              │
              └─ .Result / .Wait() / await → 2 s
                 B, C and D wait 2 s for no reason.
                 The actor is dead to the world.
```

## The fix: PipeTo

Start the async work, do **not** await it, and pipe the completed result back to yourself as a
new message. The handler returns immediately.

```
   ✅ PipeTo

   handler ── starts Task ──► (external service)
      │                              │
      └─ returns immediately         │ completes later
                                     ▼
   mailbox: [ B ][ C ][ D ][ QuoteReceived ] ◄── arrives as a normal message
                                     │
                                     └─ handled like anything else
```

```csharp
_service.FetchAsync(id).PipeTo(
    Self,
    success: result => new QuoteReceived(result),
    failure: ex     => new QuoteFailed(ex.Message));
```

## Why it is safe

The result comes back **as a message**, so it is processed by the mailbox like any other — one at
a time, on the actor's own thread. No locks, no race on your fields.

```
   ⚠️  Never touch actor state inside a ContinueWith / callback.
       That runs on a thread-pool thread, outside the mailbox guarantee.
       Always route the result back through Self.
```

## Failure is a message too

Map the exception into a normal message (`failure:`) instead of letting it escape. The actor
decides what a failure means — no restart needed for an expected error.

## Stash: keep valid messages that arrived too early

Sometimes a message is valid, but the actor cannot process it **yet**: dependencies are loading,
a `PipeTo` operation is in flight, or a child/ref has not been discovered. `Stash.Stash()` stores
the current message; when the actor becomes ready, `Stash.UnstashAll()` puts those messages back
on the mailbox in their original order.

```
Client ──ProcessItem──► Gate (Waiting)
                           │
                           │ Stash.Stash()
                           ▼
                     [in-memory stash]
                           │
              dependencies ready
                           ▼
                     Become(Ready)
                     UnstashAll()
                           │
                           ▼
Client ◄── replies ── ProcessItem handlers (FIFO)
```

Use stash when early messages should be preserved. Do not use it for long-term storage or messages
that should simply be rejected.

## Tests here

`PipeToDemoActorTests` — mailbox stays responsive while a fetch is in flight, and a failing
service surfaces as a failed-status message rather than a crash.

`StashGateActorTests` — work that arrives before the gate is ready is stashed, then unstashed and
processed in original order.

---

[← Phase 4 — Behaviour switching](../Phase4_BehaviorSwitching/README.md)  |  [Index](../README.md)  |  [Phase 6 — Routers and pipelines →](../Phase6_RoutersAndPipelines/README.md)
