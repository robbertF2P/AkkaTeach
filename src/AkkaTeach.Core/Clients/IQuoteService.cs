namespace AkkaTeach.Core.Clients;

/// <summary>
/// Stand-in for an external service (HTTP API, database, etc.) called from an actor via <c>PipeTo</c>.
/// </summary>
public interface IQuoteService
{
    Task<string> FetchQuoteAsync(string topic, CancellationToken cancellationToken = default);
}
