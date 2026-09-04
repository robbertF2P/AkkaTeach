using Akka.Hosting;
using Akka.Hosting.TestKit;
using Akka.Logger.Serilog;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.XUnit3;

namespace AkkaTeach.Tests;

/// <summary>
/// Test base that routes Akka's logging through Serilog into the xUnit test output.
/// </summary>
/// <remarks>
/// <code>
///   actor: _log.Info(...)
///        -> Akka.Logger.Serilog     (setup.AddSerilogLogging)
///        -> Serilog.Log.Logger
///        -> Serilog.Sinks.XUnit3    (ITestOutputHelper)
/// </code>
/// <para>The <see cref="ITestOutputHelper"/> must be passed to the TestKit constructor —
/// that is what connects the sink to the currently running test.</para>
/// <para>Derived classes overriding <see cref="ConfigureAkka"/> must call
/// <c>base.ConfigureAkka(builder, provider)</c> to keep logging wired up.</para>
/// </remarks>
public abstract class TeachingTestKit : TestKit
{
    protected TeachingTestKit(ITestOutputHelper output)
        : base(nameof(TeachingTestKit), output)
    {
    }

    /// <summary>Minimum Akka log level surfaced to the test output.</summary>
    protected virtual Akka.Event.LogLevel AkkaLogLevel => Akka.Event.LogLevel.InfoLevel;

    protected override void ConfigureLogging(ILoggingBuilder builder)
    {
        global::Serilog.ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.XUnit3TestOutput()
            .CreateLogger();

        global::Serilog.Log.Logger = logger;

        builder.ClearProviders();
        builder.AddSerilog(logger, dispose: true);
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.ConfigureLoggers(setup =>
        {
            setup.LogLevel = AkkaLogLevel;
            setup.AddSerilogLogging();

            // EventFilter/ExpectLogError assertions need Akka's own test listener.
            setup.AddLogger<Akka.TestKit.TestEventListener>();
        });
    }
}
