using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Akka.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;

namespace AkkaTeach.Tests.Phase2_IdentityAndLifecycle;

/// <summary>
/// PHASE 2a — Actor identity: how actors are named, addressed, and found.
/// </summary>
/// <remarks>
/// <para>Every actor has an <see cref="ActorPath"/> — a hierarchical address that looks like a URL:</para>
/// <code>akka://SystemName/user/parent/child</code>
/// <list type="bullet">
/// <item><description><c>/user</c> — the guardian that all your actors live under.</description></item>
/// <item><description>Children are nested under their parent's path.</description></item>
/// <item><description>Names must be unique among siblings.</description></item>
/// </list>
/// </remarks>
public sealed class Lesson2_ActorIdentityTests(ITestOutputHelper output) : TeachingTestKit(output)
{
    /// <summary>
    /// Actors you create live under the /user guardian.
    /// </summary>
    [Fact]
    public void TopLevelActors_LiveUnderTheUserGuardian()
    {
        IActorRef greeter = Sys.ActorOf(GreeterActor.Props(), "greeter");

        greeter.Path.ToString().Should().EndWith("/user/greeter");
        greeter.Path.Parent.Name.Should().Be("user");
    }

    /// <summary>
    /// A child's path is nested under its parent's path — the tree is visible in the address.
    /// </summary>
    [Fact]
    public async Task ChildActors_AreNestedUnderTheirParentsPath()
    {
        TestProbe watcher = CreateTestProbe();
        IActorRef parent = Sys.ActorOf(ChildSpawningParentActor.Props(watcher.Ref), "parent");

        ChildSpawnedResponse spawned =
            await parent.Ask<ChildSpawnedResponse>(new SpawnChildCommand("child"), TimeSpan.FromSeconds(3));

        spawned.Child.Path.ToString().Should().EndWith("/user/parent/child");
        spawned.Child.Path.Parent.Should().Be(parent.Path);
    }

    /// <summary>
    /// Names must be unique among siblings — reusing one is an error.
    /// </summary>
    [Fact]
    public void SiblingNames_MustBeUnique()
    {
        Sys.ActorOf(GreeterActor.Props(), "greeter");

        Action createDuplicate = () => Sys.ActorOf(GreeterActor.Props(), "greeter");

        createDuplicate.Should().Throw<InvalidActorNameException>();
    }

    /// <summary>
    /// If you do not supply a name, Akka generates one. Prefer explicit names —
    /// they show up in logs and make actors findable.
    /// </summary>
    [Fact]
    public void UnnamedActors_GetAGeneratedName()
    {
        IActorRef anonymous = Sys.ActorOf(GreeterActor.Props());

        anonymous.Path.Name.Should().NotBeNullOrEmpty();
        anonymous.Path.Name.Should().StartWith("$");
    }

    /// <summary>
    /// You can look an actor up by path with ActorSelection when you do not hold its IActorRef.
    /// Prefer passing IActorRef around; selection is for the cases where you cannot.
    /// </summary>
    [Fact]
    public void ActorSelection_FindsAnActorByPath()
    {
        Sys.ActorOf(GreeterActor.Props(), "greeter");
        TestProbe probe = CreateTestProbe();

        Sys.ActorSelection("/user/greeter").Tell(new SayHelloCommand("Eve"), probe.Ref);

        probe.ExpectMsg<HelloReply>(msg => msg.Message == "Hello, Eve!");
    }
}
