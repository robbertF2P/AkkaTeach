# Akka.Hosting cluster configuration (AkkaTeach)

Modern Akka.NET apps configure remoting and clustering **programmatically** via `AkkaConfigurationBuilder` extension methods from `Akka.Cluster.Hosting` — not by hand-editing HOCON.

Packages (beyond AkkaTeach defaults):

```xml
<PackageReference Include="Akka.Cluster.Hosting" Version="1.5.71" />
<!-- Optional for dynamic discovery instead of static seeds -->
<PackageReference Include="Akka.Management" Version="1.5.37" />
<PackageReference Include="Akka.Discovery.Kubernetes" Version="1.5.37" />
```

All nodes must use the **same actor system name** passed to `AddAkka(...)`.

---

## Single-node cluster (local dev)

```csharp
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;

builder.Services.AddAkka("AkkaTeach", (configurationBuilder, _) =>
{
    configurationBuilder
        .WithRemoting(hostname: "127.0.0.1", port: 4053)
        .WithClustering(new ClusterOptions
        {
            Roles = ["worker"],
            SeedNodes = [Address.Parse("akka.tcp://AkkaTeach@127.0.0.1:4053")],
            SplitBrainResolver = SplitBrainResolverOption.Default,
        })
        .WithActors((system, registry, resolver) =>
        {
            // register actors as usual
        });
});
```

`WithRemoting` + `WithClustering` replaces the old HOCON blocks for `akka.remote` and `akka.cluster`.

---

## Split Brain Resolver (programmatic)

```csharp
.WithClustering(new ClusterOptions
{
    SplitBrainResolver = new KeepMajorityOption { Role = "worker" },
    // or: new KeepOldestOption { Role = "worker" }
    // or: SplitBrainResolverOption.Default
});
```

Tune advanced timing via `ClusterOptions` properties (e.g. `DownRemovalMargin`, `SplitBrainResolverStableAfter`) instead of HOCON keys.

---

## Lighthouse / static seed nodes

[Lighthouse](https://github.com/petabridge/lighthouse) still provides stable seed addresses. Your **application** nodes join via programmatic `SeedNodes`:

```csharp
var lighthouseSeeds = new[]
{
    Address.Parse("akka.tcp://AkkaTeach@lighthouse-0.lighthouse:4053"),
    Address.Parse("akka.tcp://AkkaTeach@lighthouse-1.lighthouse:4053"),
};

configurationBuilder
    .WithRemoting(hostname: Environment.GetEnvironmentVariable("POD_IP") ?? "127.0.0.1", port: 4053)
    .WithClustering(new ClusterOptions
    {
        Roles = ["worker"],
        SeedNodes = lighthouseSeeds,
        SplitBrainResolver = SplitBrainResolverOption.Default,
    });
```

Lighthouse itself can still use `Akka.Bootstrap.Docker` env vars (`ACTORSYSTEM`, `CLUSTER_IP`, `CLUSTER_SEEDS`). Application services should prefer **`ClusterOptions`** in code or bind options from `IConfiguration` — not duplicate HOCON.

---

## Akka.Management + discovery (no static seeds)

For Kubernetes / cloud, clear static seeds and use cluster bootstrap:

```csharp
configurationBuilder
    .WithRemoting(hostname: "0.0.0.0", port: 4053)
    .WithClustering(new ClusterOptions
    {
        SeedNodes = [],          // required when using bootstrap
        Roles = ["worker"],
        SplitBrainResolver = SplitBrainResolverOption.Default,
    })
    .WithAkkaManagement(port: 8558)
    .WithClusterBootstrap(
        serviceName: "akka-teach",
        portName: "akka-remote",
        requiredContactPoints: 2)
    .WithKubernetesDiscovery();
```

---

## HA features via Akka.Hosting

| Feature | Extension method |
|---------|------------------|
| Cluster singleton | `.WithSingleton<TKey>(singletonName, props, options)` |
| Singleton proxy | `.WithSingletonProxy<TKey>(...)` |
| Cluster sharding | `.WithShardRegion<TKey>(typeName, propsFactory, extractor, shardOptions)` |
| Distributed pub/sub | `.WithDistributedPubSub(role)` |
| Cluster client | `.WithClusterClient<TKey>(initialContacts)` |

Example — singleton with proxy registered for DI:

```csharp
configurationBuilder
    .WithRemoting("0.0.0.0", 4053)
    .WithClustering(new ClusterOptions
    {
        Roles = ["worker"],
        SeedNodes = lighthouseSeeds,
        SplitBrainResolver = SplitBrainResolverOption.Default,
    })
    .WithSingleton<BackgroundCoordinator>(
        singletonName: "background-coordinator",
        actorProps: resolver.Props<BackgroundCoordinator>(),
        options: new ClusterSingletonOptions { Role = "worker" },
        createProxyToo: true);
```

Resolve the proxy from DI:

```csharp
var coordinator = await actorRegistry.GetAsync<BackgroundCoordinator>(cancellationToken);
coordinator.Tell(new StartWorkCommand(...));
```

---

## Binding from appsettings (still no HOCON files)

You can keep addresses out of code without HOCON:

```json
"AkkaCluster": {
  "Hostname": "127.0.0.1",
  "Port": 4053,
  "Roles": [ "worker" ],
  "SeedNodes": [
    "akka.tcp://AkkaTeach@lighthouse-0.lighthouse:4053"
  ]
}
```

```csharp
var cluster = configuration.GetSection("AkkaCluster").Get<AkkaClusterSettings>()!;

configurationBuilder
    .WithRemoting(cluster.Hostname, cluster.Port)
    .WithClustering(new ClusterOptions
    {
        Roles = cluster.Roles,
        SeedNodes = cluster.SeedNodes.Select(Address.Parse).ToArray(),
        SplitBrainResolver = SplitBrainResolverOption.Default,
    });
```

---

## When HOCON is still used

Akka.Hosting generates HOCON internally from `ClusterOptions`, `ShardOptions`, etc. Reach for raw HOCON only when:

- A setting has no Hosting API yet (rare in 1.5.x).
- You need a one-off override via `.AddHocon(...)` on the builder.

Default path: **programmatic Hosting configuration** + optional `IConfiguration` binding.

---

## Related

- [actor-model-guide.md §14](actor-model-guide.md#14-clustering-remoting-and-high-availability) — concepts (gossip, HA, Lighthouse)
- [Akka.Cluster.Hosting README](https://github.com/akkadotnet/Akka.Hosting/blob/dev/src/Akka.Cluster.Hosting/README.md)
- [Akka.Management discovery](https://getakka.net/articles/discovery/akka-management.html)
