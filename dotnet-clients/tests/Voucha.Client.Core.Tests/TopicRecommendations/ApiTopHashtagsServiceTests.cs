using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.TopicRecommendations;
using Xunit;

namespace Voucha.Client.Core.Tests.TopicRecommendations;

public sealed class ApiTopHashtagsServiceTests
{
  [Fact]
  public async Task ServiceForwardsHashtagQueriesAndTopicAliasMutations()
  {
    var handler = new RecordingHandler(
        """{"results":[{"topic_alias_id":"alias-1","hashtag":"rust","item_count":3,"contributor_count":2,"latest_content_id":"post-1","topic_id":"topic-1"}],"page_info":{"has_next_page":false},"topics":{"topic-1":{"id":"topic-1","name":"Rust","slug":"rust","topic_type":"topic"}}}""");
    var service = new ApiTopHashtagsService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var response = await service.FetchAsync("rust", TopHashtagMapping.Linked, "cursor-1", 5, TestContext.Current.CancellationToken);
    await service.LinkAsync("topic 1", "alias 1", TestContext.Current.CancellationToken);
    await service.UnlinkAsync("topic 1", "alias 1", TestContext.Current.CancellationToken);
    await service.CreateTopicAsync(new CreateTopicRequest("Rust", "rust", "topic", SourceTopicAliasId: "alias-1"), TestContext.Current.CancellationToken);

    Assert.Equal("alias-1", Assert.Single(response.Results).TopicAliasId);
    Assert.Equal("Rust", response.Topics["topic-1"].Name);
    Assert.Equal(
        [
          (HttpMethod.Get, "/api/v1/topic-recommendations/top-hashtags?after=cursor-1&limit=5&mapping=linked&q=rust"),
          (HttpMethod.Post, "/api/v1/topics/topic%201/aliases/alias%201"),
          (HttpMethod.Delete, "/api/v1/topics/topic%201/aliases/alias%201"),
          (HttpMethod.Post, "/api/v1/topics"),
        ],
        handler.Requests);
    Assert.Contains("source_topic_alias_id", handler.Bodies[3], StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(TopHashtagMapping.All, "all")]
  [InlineData(TopHashtagMapping.Linked, "linked")]
  public void EndpointMapsEverySupportedFilter(TopHashtagMapping mapping, string value) =>
      Assert.Equal(value, VouchaApiEndpoints.TopHashtags(mapping: mapping).Query["mapping"]);

  private sealed class RecordingHandler(string response) : HttpMessageHandler
  {
    public List<(HttpMethod Method, string? PathAndQuery)> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Requests.Add((request.Method, request.RequestUri?.PathAndQuery));
      Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response), RequestMessage = request };
    }
  }
}
