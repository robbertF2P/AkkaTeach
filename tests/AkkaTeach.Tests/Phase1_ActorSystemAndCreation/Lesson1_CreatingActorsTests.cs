using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Akka.Logger.Serilog;
using Akka.TestKit;
using AkkaTeach.Contracts;
using AkkaTeach.Core.Actors;
using FluentAssertions;
using Serilog;
using Serilog.Sinks.XUnit3;

namespace AkkaTeach.Tests.Phase1_ActorSystemAndCreation;

/// <summary>
/// PHASE 1 — What is an actor system, and how do you create actors?
/// </summary>
/// <remarks>
/// <para><b>Read these tests top to bottom. They answer, in order:</b></para>
/// <list type="number">
/// <item><description>What is the <c>ActorSystem</c>?</description></item>
/// <item><description>How do I describe an actor before creating it? (<c>Props</c>)</description></item>
/// <item><description>How do I actually create one? (<c>ActorOf</c>)</description></item>
/// <item><description>What do I get back? (<c>IActorRef</c> — a handle, never the instance)</description></item>
/// <item><description>How do actors create other actors? (<c>Context.ActorOf</c>)</description></item>
/// </list>
/// <para><b>The one rule to take away:</b> you never touch an actor object directly.
/// You describe it with <c>Props</c>, ask the system to create it, and talk to the
/// <see cref="IActorRef"/> you get back.</para>
/// </remarks>
public sealed class Lesson1_CreatingActorsTests(ITestOutputHelper output) : TeachingTestKit(output)
{
    /// <summary>
    /// 1. The ActorSystem is the container every actor lives in.
    /// It owns the threads, the mailboxes, and the address space.
    /// </summary>
    [Fact]
    public void ActorSystem_IsTheContainerThatOwnsAllActors()
    {
        // 'Sys' is the ActorSystem the TestKit created for this test.
        Sys.Should().NotBeNull();
        Sys.Name.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 2. Props is a *recipe* for an actor, not the actor itself.
    /// Creating Props does not start anything — nothing is running yet.
    /// </summary>
    [Fact]
    public void Props_IsOnlyARecipe_NoActorExistsYet()
    {
        Props recipe = GreeterActor.Props();

        recipe.Should().NotBeNull();
        recipe.Type.Should().Be(typeof(GreeterActor));
    }

    /// <summary>
    /// 3. ActorOf turns the recipe into a live actor and returns a handle to it.
    /// </summary>
    [Fact]
    public void ActorOf_CreatesTheActor_AndReturnsAHandle()
    {
        IActorRef greeter = Sys.ActorOf(GreeterActor.Props(), "greeter");

        greeter.Should().NotBeNull();
        greeter.Path.Name.Should().Be("greeter");
    }

    /// <summary>
    /// 4. You get an IActorRef — never the GreeterActor instance.
    /// This is deliberate: it is what stops you calling methods on actor state
    /// from another thread. The only thing you can do to an actor is send it a message.
    /// </summary>
    [Fact]
    public void WhatYouGetBack_IsAReference_NotTheActorInstance()
    {
        IActorRef greeter = Sys.ActorOf(GreeterActor.Props(), "greeter");

        greeter.Should().BeAssignableTo<IActorRef>();
        greeter.Should().NotBeAssignableTo<GreeterActor>();
    }

    /// <summary>
    /// 5. Sending a message is the only way to interact. Here we send one and get a reply,
    /// which proves the actor really is alive and processing its mailbox.
    /// </summary>
    [Fact]
    public void TheOnlyWayToInteract_IsToSendAMessage()
    {
        IActorRef greeter = Sys.ActorOf(GreeterActor.Props(), "greeter");
        TestProbe probe = CreateTestProbe();

        greeter.Tell(new SayHelloCommand("Alice"), probe.Ref);

        probe.ExpectMsg<HelloReply>(msg => msg.Message == "Hello, Alice!");
    }

    /// <summary>
    /// 6. Actors create other actors with Context.ActorOf, which makes a parent/child pair.
    /// AddressingDemoActor creates a "greeter" child in its constructor, so the child's
    /// address sits underneath the parent's address.
    /// </summary>
    [Fact]
    public void ActorsCreateOtherActors_FormingAParentChildTree()
    {
        IActorRef frontDesk = Sys.ActorOf(AddressingDemoActor.Props(), "front-desk");
        TestProbe probe = CreateTestProbe();

        // The parent delegates to the child it created internally.
        frontDesk.Tell(new AskViaTellCommand("Dave"), probe.Ref);

        probe.ExpectMsg<HelloReply>(msg => msg.Message == "Hello, Dave!");
    }

    /// <summary>
    /// 7. Two actors from the same Props are two completely separate actors
    /// with separate state and separate mailboxes. Props is a recipe, not a singleton.
    /// </summary>
    [Fact]
    public void SameProps_CreatesIndependentActors()
    {
        IActorRef first = Sys.ActorOf(WorkCoordinatorActor.Props(), "coordinator-one");
        IActorRef second = Sys.ActorOf(WorkCoordinatorActor.Props(), "coordinator-two");
        TestProbe probe = CreateTestProbe();

        // Give work to the first one only.
        first.Tell(new ProcessWorkItemCommand("a", 1), probe.Ref);
        probe.ExpectMsg<WorkItemProcessed>();

        // The first counted it...
        first.Tell(new GetCompletedCountQuery(), probe.Ref);
        probe.ExpectMsg<CompletedCountResponse>(msg => msg.Count == 1);

        // ...the second knows nothing about it. Separate state.
        second.Tell(new GetCompletedCountQuery(), probe.Ref);
        probe.ExpectMsg<CompletedCountResponse>(msg => msg.Count == 0);
    }
}
