# Phase 2 — Identity and lifecycle

**Questions this phase answers:** how is an actor addressed, and what happens when it starts,
fails, or stops?

## 2a — Identity: every actor has a path

```
   akka://AkkaTeach/user/parent/child
   └──┬──┘ └───┬───┘ └─┬─┘ └──┬───┘
   protocol  system  guardian  your actors, nested by parent
```

```
   /
   ├── /user        ← everything you create lives here
   │     ├── greeter
   │     └── parent
   │           └── child
   ├── /system      ← Akka's own internals
   └── /deadLetters ← where messages to dead actors go
```

Rules: names must be unique among siblings; omit a name and Akka generates one like `$a`;
`ActorSelection("/user/greeter")` looks one up by path when you do not hold its `IActorRef`.

## 2b — Lifecycle: the hooks

```
   ActorOf
      │
      ▼
   PreStart ──► handling messages ──► PostStop
                     │                   ▲
                     │ throws            │ Stop / PoisonPill
                     ▼                   │
                  parent decides ────────┘
                     │
                     │ Restart (the default)
                     ▼
   PreRestart ─► PostStop ─► PostRestart ─► PreStart ─► handling messages again
   └───────────────────────────┬──────────────────────┘
        same IActorRef, same mailbox, NEW instance, state lost
```

> The `PostStop` and `PreStart` in the middle surprise people. The default `PreRestart` calls
> `PostStop`, and the default `PostRestart` calls `PreStart` — so your cleanup and setup both
> run around a restart.

## Restart vs stop

| | Restart | Stop |
|---|---|---|
| Instance | replaced | gone |
| `IActorRef` | still valid | messages go to dead letters |
| In-memory state | **lost** | gone |
| Watchers notified | **no** | yes (`Terminated`) |
| Default on exception | ✅ | only if the parent says so |

## Supervision: the parent decides

```
        parent
          │  SupervisorStrategy:
          │  Restart │ Stop │ Resume │ Escalate
          ▼
        child ──► throws ──► exception goes to parent, not to the caller
```

An exception does **not** propagate to whoever sent the message. It goes up the tree.
Stopping a parent stops its whole subtree.

## Death watch

```
   watcher ──Watch──► target
                        │ stops
                        ▼
   watcher ◄──Terminated──
```

## Tests here

- `Lesson2_ActorIdentityTests` — guardian, nesting, unique names, generated names, `ActorSelection`
- `Lesson3_ActorLifecycleTests` — hooks, restart-by-default, state loss, `Directive.Stop` override,
  death watch, restart-is-not-termination, parent teardown

---

[← Phase 1 — Actor system and creation](../Phase1_ActorSystemAndCreation/README.md)  |  [Index](../README.md)  |  [Phase 3 — Messaging →](../Phase3_Messaging/README.md)
