using AkkaTeach.Contracts;

namespace AkkaTeach.Core.Clients;

/// <summary>
/// Port for an external paginated data API. Actors depend on this abstraction;
/// production would use HttpClient, tests and demos use <see cref="MockDataApiClient"/>.
/// </summary>
public interface IDataApiClient
{
    Task<ApiDataPage> FetchPageAsync(int pageNumber, CancellationToken cancellationToken = default);
}
