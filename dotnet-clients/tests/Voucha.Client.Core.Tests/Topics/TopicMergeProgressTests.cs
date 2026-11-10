using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicMergeProgressTests
{
  public static TheoryData<string, string, bool> Fixtures => new()
  {
    { "native.topics.merge.accepted", nameof(UiMessageKey.SettingsTopicsMergeQueued), true },
    { "native.topics.merge.status.queued", nameof(UiMessageKey.SettingsTopicsMergeQueued), true },
    { "native.topics.merge.status.running", nameof(UiMessageKey.SettingsTopicsMergeQueued), true },
    { "native.topics.merge.status.completed", nameof(UiMessageKey.SettingsTopicsMergeCompleted), false },
    { "native.topics.merge.status.failed", nameof(UiMessageKey.SettingsTopicsMergeFailed), false },
  };

  [Theory]
  [MemberData(nameof(Fixtures))]
  public void FixtureStatusMapsToMergeMessage(string fixtureId, string expectedKeyName, bool canCheck)
  {
    var operation = Operation(fixtureId);

    Assert.Equal(KeyByName(expectedKeyName), TopicMergeProgress.MessageFor(operation));
    Assert.Equal(canCheck, TopicMergeProgress.CanCheckStatus(operation));
  }

  [Fact]
  public void UnknownStatusIsNeverTreatedAsCompleted()
  {
    var unknown = Operation("native.topics.merge.accepted") with { Status = "unexpected" };

    Assert.Equal(UiMessageKey.SettingsTopicsMergeQueued, TopicMergeProgress.MessageFor(unknown));
    Assert.True(TopicMergeProgress.CanCheckStatus(unknown));
  }

  [Fact]
  public void RejectsMissingOperation()
  {
    Assert.Throws<ArgumentNullException>(() => TopicMergeProgress.MessageFor(null!));
    Assert.Throws<ArgumentNullException>(() => TopicMergeProgress.CanCheckStatus(null!));
  }

  [Fact]
  public async Task ServiceReadsMergeStatusFromOperationEndpoint()
  {
    var handler = new FixtureHandler(ApiFixtureLoader.LoadResponse("native.topics.merge.status.running"));
    var service = new ApiTopicsService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var response = await service.FetchTopicMergeStatusAsync("topic 1", "op 1", TestContext.Current.CancellationToken);

    Assert.Equal("running", response.TopicMerge.Status);
    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal("/api/v1/topics/topic%201/merges/op%201", handler.PathAndQuery);
  }

  [Fact]
  public async Task DefaultInterfaceMergeStatusIsUnsupported()
  {
    ITopicsService service = new MinimalTopicsService();

    await Assert.ThrowsAsync<NotSupportedException>(
        () => service.FetchTopicMergeStatusAsync("topic-1", "op-1", TestContext.Current.CancellationToken));
  }

  private static TopicMergeDetails Operation(string fixtureId) =>
      JsonSerializer.Deserialize<TopicMergeResponse>(
          ApiFixtureLoader.LoadResponse(fixtureId),
          VouchaApiJson.Options)!.TopicMerge;

  private static UiMessageKey KeyByName(string name) =>
      (UiMessageKey)typeof(UiMessageKey).GetField(name)!.GetValue(null)!;

  private sealed class FixtureHandler(string body) : HttpMessageHandler
  {
    public HttpMethod? Method { get; private set; }
    public string? PathAndQuery { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Method = request.Method;
      PathAndQuery = request.RequestUri?.PathAndQuery;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body),
        RequestMessage = request,
      });
    }
  }

  private sealed class MinimalTopicsService : ITopicsService
  {
    public Task<TopicSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicResponse> FetchTopicAsync(string topicIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicMutationResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicMutationResponse> UpdateTopicAsync(string topicIdOrSlug, UpdateTopicBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<RssFeedsResponse> FetchRssFeedsForTopicAsync(string topicId, RssFeedEnabledFilter? enabled = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task FollowTopicAsync(string topicId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UnfollowTopicAsync(string topicId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task FollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UnfollowSourceAsync(string rssFeedId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UpdateSourceAsync(string rssFeedId, UpdateRssFeedBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ListResponse<TopicAlias>> FetchTopicAliasesAsync(string topicId, string? after = null, int? limit = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ListResponse<TopicAdditionalHostname>> FetchTopicAdditionalHostnamesAsync(string topicId, string? after = null, int? limit = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CreateTopicAliasesAsync(string topicId, CreateTopicAliasesBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteTopicAliasAsync(string topicId, string aliasId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicAdditionalHostnameResponse> CreateTopicAdditionalHostnameAsync(string topicId, string hostname, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteTopicAdditionalHostnameAsync(string topicId, string hostnameId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TopicMergeResponse> MergeTopicAliasesAsync(string sourceTopicId, string destinationIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}
