# Phase 4 — Behaviour switching

**Question this phase answers:** how do I model state without `if (_state == ...)` everywhere?

An actor can swap its whole set of message handlers at runtime with `Become`. Each state is a
method that registers only the handlers that are legal in that state.

```
                 StartSession
        ┌────────────────────────┐
        │                        ▼
   ┌─────────┐             ┌──────────┐
   │  Idle   │             │  Active  │◄──┐
   └─────────┘             └──────────┘   │ RecordProgress
        ▲                        │        │ (stays in Active)
        │                        │        └──┘
        │ ResetSession           │ EndSession
        │                        ▼
        │                  ┌───────────┐
        └──────────────────│ Completed │
                           └───────────┘
```

## Why this beats a status field

```
   ❌ one Receive, every handler guarded        ✅ one Receive set per state
   Receive<RecordProgress>(m => {                Become(Active);
       if (_state != Active) return;             // RecordProgress only exists here,
       ...                                       // so illegal messages are simply
   });                                           // unhandled — no guard needed
```

- Illegal messages in a state are **not handled**, rather than silently ignored by a guard.
- Each state method reads as a small, complete description of what is possible right now.
- `Become` replaces the behaviour; `Unbecome` pops back to the previous one (stack mode).

> Behaviour switching changes *handlers*, not identity. Same `IActorRef`, same mailbox —
> unlike a restart (Phase 2), which replaces the instance.

## Tests here

`SessionActorTests` — initial state, each transition, rejection of illegal commands in
`Completed`, and the events published along the way.

---

[← Phase 3 — Messaging](../Phase3_Messaging/README.md)  |  [Index](../README.md)  |  [Phase 5 — Async work →](../Phase5_AsyncWork/README.md)
