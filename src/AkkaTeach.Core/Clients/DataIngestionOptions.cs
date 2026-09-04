namespace AkkaTeach.Core.Clients;

/// <summary>
/// Configuration for the mock API and ingestion worker pool.
/// </summary>
public sealed class DataIngestionOptions
{
    public const string SectionName = "DataIngestion";

    public int WorkerPoolSize { get; set; } = 4;

    public int PageSize { get; set; } = 50;

    public int TotalPages { get; set; } = 10;

    /// <summary>
    /// Simulated network latency per page fetch.
    /// </summary>
    public int FetchDelayMilliseconds { get; set; } = 100;
}
