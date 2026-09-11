using Akka.Hosting;
using Akka.Hosting.TestKit;

namespace AkkaTeach.Tests;

/// <summary>
/// Shared test base for the course's Akka.Hosting TestKit tests.
/// </summary>
/// <remarks>
/// Akka.Hosting.TestKit owns the Microsoft.Extensions.Logging integration and test output
/// plumbing. This base only keeps the Akka test event listener enabled for log assertions.
/// <para>Derived classes overriding <see cref="ConfigureAkka"/> must call
/// <c>base.ConfigureAkka(builder, provider)</c> to retain the test listener.</para>
/// </remarks>
public abstract class TeachingTestKit : TestKit
{
    protected TeachingTestKit(ITestOutputHelper output)
        : base(nameof(TeachingTestKit), output)
    {
    }

    /// <summary>Minimum Akka log level surfaced to the test output.</summary>
    protected virtual Akka.Event.LogLevel AkkaLogLevel => Akka.Event.LogLevel.InfoLevel;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.ConfigureLoggers(setup =>
        {
            setup.LogLevel = AkkaLogLevel;

            // EventFilter/ExpectLogError assertions need Akka's own test listener.
            setup.AddLogger<Akka.TestKit.TestEventListener>();
        });
    }
}
