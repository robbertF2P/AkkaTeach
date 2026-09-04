using AkkaTeach.ConsoleApp;
using AkkaTeach.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Warning()
    .CreateLogger();

try
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Services.AddSerilog();
    builder.Services.AddAkkaTeachActors(builder.Configuration);
    builder.Services.AddSingleton<TeachingConsole>();

    IHost host = builder.Build();
    await host.StartAsync();

    try
    {
        TeachingConsole console = host.Services.GetRequiredService<TeachingConsole>();
        await console.RunAsync(CancellationToken.None);
    }
    finally
    {
        await host.StopAsync();
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "AkkaTeach console terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
