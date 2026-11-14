using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class CommunityAgentPromptContractTests
{
  [Fact]
  public void LandedFixtureRequiresPaginationSlotAndPromptFacts()
  {
    var fixture = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.communities.agent-prompts.default"))!;
    var paths = new List<string> { "community_agent_prompts", "slot_info", "page_info" };
    paths.AddRange(fixture["page_info"]!.AsObject().Select(pair => $"page_info.{pair.Key}"));
    paths.AddRange(fixture["slot_info"]!.AsObject().Select(pair => $"slot_info.{pair.Key}"));
    paths.AddRange(fixture["slot_info"]!["limits_by_plan"]!.AsObject().Select(pair => $"slot_info.limits_by_plan.{pair.Key}"));
    paths.AddRange(fixture["community_agent_prompts"]![0]!.AsObject().Select(pair => $"community_agent_prompts.0.{pair.Key}"));
    foreach (var path in paths)
    {
      var copy = fixture.DeepClone();
      var parts = path.Split('.');
      var parent = copy;
      foreach (var part in parts[..^1]) parent = parent is JsonArray array ? array[int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)]! : parent[part]!;
      parent.AsObject().Remove(parts[^1]);
      Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CommunityAgentPromptsResponse>(copy.ToJsonString(), VouchaApiJson.Options));
    }
    foreach (var name in new[] { "community_agent_prompts", "slot_info", "page_info" })
    {
      var copy = fixture.DeepClone();
      copy[name] = null;
      Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CommunityAgentPromptsResponse>(copy.ToJsonString(), VouchaApiJson.Options));
    }
    fixture["community_agent_prompts"]![0]!["created_by_id"] = null;
    var decoded = JsonSerializer.Deserialize<CommunityAgentPromptsResponse>(fixture.ToJsonString(), VouchaApiJson.Options)!;
    var roundTrip = JsonSerializer.Deserialize<CommunityAgentPromptsResponse>(JsonSerializer.Serialize(decoded, VouchaApiJson.Options), VouchaApiJson.Options)!;
    Assert.Null(roundTrip.CommunityAgentPrompts[0].CreatedById);
    Assert.Equal(new CommunityAgentPromptSlotInfo(1, 3, 2, new(3, 10)), roundTrip.SlotInfo);
    Assert.Null(roundTrip.PageInfo.EndCursor);
    Assert.False(roundTrip.PageInfo.HasNextPage);
  }

  [Fact]
  public async Task ClientServiceForwardsBoundedOpaqueCursorAndLeavesHistoryContractAlone()
  {
    var handler = new Handler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var client = new VouchaApiClient(http);
    var service = new ApiCommunitiesService(client);
    await service.FetchAgentPromptsPageAsync("test community", "opaque/+?=", 7, TestContext.Current.CancellationToken);
    await client.FetchCommunityAgentPromptsAsync("test community", limit: 1000, cancellationToken: TestContext.Current.CancellationToken);
    await client.FetchCommunityAgentPromptHistoryAsync("test community", "prompt /1", "older/+?", TestContext.Current.CancellationToken);
    Assert.Contains("limit=7", handler.Paths[0], StringComparison.Ordinal);
    Assert.Contains("after=opaque%2F%2B%3F%3D", handler.Paths[0], StringComparison.Ordinal);
    Assert.Contains("limit=100", handler.Paths[1], StringComparison.Ordinal);
    Assert.DoesNotContain("after=", handler.Paths[1], StringComparison.Ordinal);
    Assert.StartsWith("/api/v1/communities/test%20community/agent-prompts/history?", handler.Paths[2], StringComparison.Ordinal);
    Assert.Contains("promptId=prompt%20%2F1", handler.Paths[2], StringComparison.Ordinal);
    Assert.Contains("before=older%2F%2B%3F", handler.Paths[2], StringComparison.Ordinal);
    Assert.DoesNotContain("after=", handler.Paths[2], StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(null, null)]
  [InlineData(0, "25")]
  [InlineData(-10, "25")]
  [InlineData(7, "7")]
  [InlineData(1000, "100")]
  public void PageLimitsUseProducerDefaultAndMaximum(int? limit, string? expected)
  {
    var request = VouchaApiEndpoints.CommunityAgentPrompts("community", limit: limit);
    if (expected is null) Assert.DoesNotContain("limit", request.Query);
    else Assert.Equal(expected, request.Query["limit"]);
  }

  private sealed class Handler : HttpMessageHandler
  {
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(request.RequestUri.AbsolutePath.EndsWith("/history", StringComparison.Ordinal)
            ? "{\"entries\":[],\"next_cursor\":null}" : ApiFixtureLoader.LoadResponse("web.communities.agent-prompts.default")),
      });
    }
  }
}
