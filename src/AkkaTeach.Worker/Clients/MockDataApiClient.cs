using AkkaTeach.Contracts;
using AkkaTeach.Core.Clients;
using Microsoft.Extensions.Options;

namespace AkkaTeach.Worker.Clients;

/// <summary>
/// Simulates a paginated REST API that returns many records across multiple pages.
/// Replace with HttpClient in production; actors stay unchanged.
/// </summary>
public sealed class MockDataApiClient : IDataApiClient
{
    private readonly DataIngestionOptions _options;
    private readonly ILogger<MockDataApiClient> _logger;

    public MockDataApiClient(IOptions<DataIngestionOptions> options, ILogger<MockDataApiClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ApiDataPage> FetchPageAsync(int pageNumber, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1 || pageNumber > _options.TotalPages)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page is out of range.");
        }

        await Task.Delay(_options.FetchDelayMilliseconds, cancellationToken);

        List<ExternalDataRecord> records = Enumerable.Range(1, _options.PageSize)
            .Select(index => new ExternalDataRecord(
                Id: $"page-{pageNumber:D3}-item-{index:D4}",
                Value: (pageNumber * 1000) + index,
                Source: "mock-api"))
            .ToList();

        _logger.LogDebug(
            "Mock API returned page {Page}/{TotalPages} with {Count} records",
            pageNumber,
            _options.TotalPages,
            records.Count);

        return new ApiDataPage(pageNumber, _options.TotalPages, records);
    }
}
