using AkkaTeach.Contracts;
using AkkaTeach.Core.Clients;
using AkkaTeach.Worker.Clients;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AkkaTeach.Tests.Phase6_RoutersAndPipelines;

public sealed class MockDataApiClientTests
{
    [Fact]
    public async Task FetchPageAsync_ReturnsConfiguredPageSize()
    {
        var client = new MockDataApiClient(
            Options.Create(new DataIngestionOptions
            {
                PageSize = 25,
                TotalPages = 5,
                FetchDelayMilliseconds = 0,
            }),
            NullLogger<MockDataApiClient>.Instance);

        var page = await client.FetchPageAsync(3);

        page.PageNumber.Should().Be(3);
        page.TotalPages.Should().Be(5);
        page.Records.Should().HaveCount(25);
        page.Records[0].Id.Should().StartWith("page-003-item-");
    }

    [Fact]
    public async Task FetchPageAsync_ThrowsWhenPageOutOfRange()
    {
        var client = new MockDataApiClient(
            Options.Create(new DataIngestionOptions { TotalPages = 3, FetchDelayMilliseconds = 0 }),
            NullLogger<MockDataApiClient>.Instance);

        var act = () => client.FetchPageAsync(4);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
