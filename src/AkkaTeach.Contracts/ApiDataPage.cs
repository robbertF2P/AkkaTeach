namespace AkkaTeach.Contracts;

/// <summary>
/// One page of results from a paginated API.
/// </summary>
public sealed record ApiDataPage(int PageNumber, int TotalPages, IReadOnlyList<ExternalDataRecord> Records);
