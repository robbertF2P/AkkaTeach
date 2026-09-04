# Phase 1 — Actor system and actor creation

**Question this phase answers:** what actually exists at runtime, and what do I hold in my hand?

```
                    ActorSystem
   ┌──────────────────────────────────────────────┐
   │                                              │
   │   Props ──── ActorOf ────► ┌──────────────┐  │
   │  (recipe)                  │  actor       │  │
   │                            │  ┌────────┐  │  │
   │   IActorRef ──── Tell ───► │  │mailbox │  │  │
   │   (address)                │  └───┬────┘  │  │
   │                            │      ▼       │  │
   │                            │   Receive    │  │
   │                            │   + state    │  │
   │                            └──────────────┘  │
   └──────────────────────────────────────────────┘
```

## The model

| Thing | What it is | What it is *not* |
|---|---|---|
| `ActorSystem` | The container. Owns threads, config, the actor tree. One per app. | Not per-request. Expensive to create. |
| `Props` | A recipe describing *how* to build an actor. Inert. | Not an actor. Creating Props starts nothing. |
| `ActorOf` | Hands the recipe to the system, which builds and starts the actor. | — |
| `IActorRef` | An address you send messages to. | **Not** the actor instance. You can never call its methods. |

## Why the reference matters

Because you only hold an address, Akka is free to restart the actor, move it to another machine,
or queue your message — and your code does not change. That indirection is the whole point.

```
   you ──► IActorRef ──► mailbox ──► actor instance
                                     (may be replaced at any time)
```

## Tree

Actors create other actors via `Context.ActorOf`, so the system is a tree, not a flat bag.

```
   /user
     └── coordinator
           └── processor
```

## Tests here

`Lesson1_CreatingActorsTests` — system as container, Props inertness, ActorOf, ref-not-instance,
message-only interaction, parent/child, independent instances from one Props.

---

[← Course index](../README.md)  |  [Index](../README.md)  |  [Phase 2 — Identity and lifecycle →](../Phase2_IdentityAndLifecycle/README.md)
