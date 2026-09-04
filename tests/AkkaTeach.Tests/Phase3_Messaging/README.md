# Phase 3 — Messaging

**Question this phase answers:** how do actors actually talk to each other?

## Tell vs Ask

```
   Tell — fire and forget, the normal case
   caller ──message──► actor
                        └─ optionally replies to Sender

   Ask — request/response, returns a Task
   caller ──message──► actor
          ◄──reply────┘
   (Akka creates a hidden temp actor to catch the reply; always give it a timeout)
```

Prefer `Tell`. Use `Ask` only at the edge, where non-actor code needs an answer.

## Sender: who gets the reply

Every message carries an implicit `Sender`. `Sender.Tell(...)` replies to whoever sent it.

```
   Tell (default sender)          Forward (preserves original sender)
   caller ──► frontdesk           caller ──► frontdesk
                 │ Tell                          │ Forward
                 ▼                               ▼
              greeter                        greeter
                 │ Sender = frontdesk           │ Sender = caller
                 ▼                               ▼
             frontdesk                        caller  ◄── reply lands directly
```

- `Tell` — the receiver sees *you* as `Sender`.
- `Tell(msg, originalSender)` — you can pass the sender along explicitly.
- `Forward` — shorthand for the same thing; the middleman disappears from the reply path.

## Parent/child delegation

`WorkCoordinatorActor` keeps its child's `IActorRef` in a field, set once at construction.

```
   caller ──► coordinator ──Forward──► processor
          ◄────────────────reply──────┘
                  │
                  └──publish──► EventStream ──► any subscriber
```

## Peer mesh: no shared parent needed

An `IActorRef` is just an address, so any actor can message any other once it has been handed
the reference. There is no requirement to route through a common ancestor.

```
      A ◄────► B
      ▲ ╲    ╱ ▲
      │  ╲  ╱  │
      │   ╳    │
      │  ╱  ╲  │
      ▼ ╱    ╲ ▼
      C ◄────► D
```

## EventStream: broadcast without knowing the listeners

```
   actor ──Publish──► EventStream ──► subscriber 1
                                 ├──► subscriber 2
                                 └──► subscriber 3
```

Use it for notifications where the sender should not care who is listening.

## Tests here

- `AddressingDemoActorTests` — Sender semantics, Tell vs Forward, child addressing
- `PeerActorTests` — direct peer messaging, cross-mesh, full-mesh wiring
- `WorkCoordinatorActorTests` — delegation to a child, state, EventStream publishing

---

[← Phase 2 — Identity and lifecycle](../Phase2_IdentityAndLifecycle/README.md)  |  [Index](../README.md)  |  [Phase 4 — Behaviour switching →](../Phase4_BehaviorSwitching/README.md)
