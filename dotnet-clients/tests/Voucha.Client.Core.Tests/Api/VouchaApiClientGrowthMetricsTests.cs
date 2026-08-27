using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchGrowthMetricsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.growth-metrics.default");

    var response = await client.FetchGrowthMetricsAsync(
        GrowthMetricsRange.ThirtyDays,
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/growth-metrics?range=30d");
    Assert.Equal("30d", response.Range);
    Assert.Equal(1200, response.UserGrowth.TotalUsers);
    Assert.Equal(80, response.ContentProduction.PostsByType.DataPoint);
    Assert.Equal(2100, response.Engagement.VotesCast);
    Assert.Equal(2.4, response.NetworkEffects.ReferralCoefficient);
    Assert.Equal(60, response.Revenue.MembershipsByTier["plus"]);
    Assert.Equal(14200, response.Infrastructure.QueueThroughput);
  }

  [Fact]
  public void GrowthMetricsResponseDecodesLargeInfrastructureCounters()
  {
    var json = ApiFixtureLoader.LoadResponse("web.growth-metrics.default")
        .Replace("\"queue_throughput\": 14200", "\"queue_throughput\": 3000000000", StringComparison.Ordinal)
        .Replace("\"ai_token_usage\": 4800000", "\"ai_token_usage\": 4000000000", StringComparison.Ordinal);

    var response = JsonSerializer.Deserialize<GrowthMetricsResponse>(json, VouchaApiJson.Options);

    Assert.Equal(3_000_000_000L, response?.Infrastructure.QueueThroughput);
    Assert.Equal(4_000_000_000L, response?.Infrastructure.AiTokenUsage);
  }

  [Theory]
  [InlineData("today")]
  [InlineData("7d")]
  [InlineData("30d")]
  [InlineData("90d")]
  [InlineData("all")]
  public void GrowthMetricsRangeTryParseAcceptsKnownRanges(string value)
  {
    Assert.True(GrowthMetricsRange.TryParse(value, out var range));
    Assert.Equal(value, range.Value);
  }

  [Fact]
  public void GrowthMetricsRangeTryParseRejectsUnknownRanges()
  {
    Assert.False(GrowthMetricsRange.TryParse("365d", out var range));
    Assert.True(string.IsNullOrWhiteSpace(range.Value));
  }

  [Fact]
  public async Task FetchGrowthMetricsAsyncDefaultsToThirtyDays()
  {
    var (client, handler) = CreateClient("web.growth-metrics.default");

    await client.FetchGrowthMetricsAsync(cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/growth-metrics?range=30d");
  }

  [Fact]
  public async Task FetchGrowthMetricsAsyncForwardsRange()
  {
    var (client, handler) = CreateClient("web.growth-metrics.default");

    await client.FetchGrowthMetricsAsync(
        GrowthMetricsRange.SevenDays,
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/growth-metrics?range=7d");
  }
}
