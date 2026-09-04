# AkkaTeach tests — read them in order

The tests are the course. Each phase folder builds on the one before it, so start at Phase 1
and read the XML doc comments as you go — they carry the explanation, the tests carry the proof.

**Each phase folder has its own `README.md` with a diagram** — those are the ones to pull up
when discussing a concept.

## The arc

```
   1  what exists          ActorSystem · Props · ActorOf · IActorRef
          │
   2  who is it, how long  ActorPath · PreStart/PostStop · restart vs stop · supervision
          │
   3  how do they talk     Tell · Ask · Sender · Forward · EventStream
          │
   4  how do they change   Become / Unbecome — state as behaviour
          │
   5  how do they wait     PipeTo — async without blocking the mailbox
          │
   6  how does it scale    routers · fan-out/aggregate · injected IO
          │
   7  how does it ship     Akka.Hosting · DI · ActorRegistry
```

| Phase | Folder | What you learn |
|-------|--------|----------------|
| 1 | [`Phase1_ActorSystemAndCreation`](./Phase1_ActorSystemAndCreation/README.md) | What an `ActorSystem` is, `Props` as a recipe, `ActorOf`, why you only ever hold an `IActorRef` |
| 2 | [`Phase2_IdentityAndLifecycle`](./Phase2_IdentityAndLifecycle/README.md) | `ActorPath` and the `/user` hierarchy, naming, `ActorSelection`; then `PreStart`/`PostStop`/`PreRestart`/`PostRestart`, restart vs stop, supervision, death watch |
| 3 | [`Phase3_Messaging`](./Phase3_Messaging/README.md) | `Tell` vs `Ask`, `Sender`, `Self`, actors talking to each other, parent/child delegation |
| 4 | [`Phase4_BehaviorSwitching`](./Phase4_BehaviorSwitching/README.md) | `Become`/`Unbecome` — modelling state as behaviour instead of `if` chains |
| 5 | [`Phase5_AsyncWork`](./Phase5_AsyncWork/README.md) | `PipeTo` — doing async work without blocking the mailbox |
| 6 | [`Phase6_RoutersAndPipelines`](./Phase6_RoutersAndPipelines/README.md) | Fan-out, aggregation, and the IO boundary behind an injected client |
| 7 | [`Phase7_Hosting`](./Phase7_Hosting/README.md) | Wiring actors into a .NET host with `Akka.Hosting` and DI |

## The three ideas that unlock everything else

1. **You never touch an actor instance.** `ActorOf` gives you an `IActorRef` — an address. The only
   way to interact is to send it a message.
2. **One message at a time.** The mailbox serialises delivery, so plain instance fields are safe
   state. No locks, no `Interlocked`.
3. **Failure is a message too.** An exception does not kill an actor; it goes to the parent, which
   decides Restart / Stop / Resume / Escalate.

## Running

```bash
cd AkkaTeach
dotnet test tests/AkkaTeach.Tests/AkkaTeach.Tests.csproj
```
