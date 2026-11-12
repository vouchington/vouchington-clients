using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class RelationLimitTests
{
  private const string LimitBody = "{\"error\":{\"code\":\"RELATION_LIMIT_REACHED\",\"message\":\"server copy\"}}";

  [Theory]
  [InlineData(HttpStatusCode.Conflict, "RELATION_LIMIT_REACHED", true)]
  [InlineData(HttpStatusCode.BadRequest, "RELATION_LIMIT_REACHED", false)]
  [InlineData(HttpStatusCode.Conflict, "OTHER", false)]
  public void RefusalRequiresExactStatusAndCode(HttpStatusCode status, string code, bool expected)
  {
    var error = new VouchaApiException(status, $"{{\"error\":{{\"code\":\"{code}\"}}}}");
    Assert.Equal(expected, error.IsRelationLimitReached);
    Assert.Equal(expected ? UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpActionFailed) : error.Message,
        RelationMutationErrors.Message(error, UiLocalization.English));
  }

  [Fact]
  public async Task PostSubscriptionPreservesTypedRefusalInsteadOfReturningSuccess()
  {
    using var client = new HttpClient(new RefusalHandler()) { BaseAddress = new("https://api.test") };
    var api = new VouchaApiClient(client);
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => api.SendAsync(
        VouchaApiEndpoints.Bookmark("post", "post-1", BookmarkPredicate.Subscribe.ToApiName()), TestContext.Current.CancellationToken));
    Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
    Assert.Equal("RELATION_LIMIT_REACHED", error.ErrorCode);
    Assert.True(error.IsRelationLimitReached);
  }

  [Fact]
  public async Task TopicAndSourceRefusalsDoNotPublishFollowSuccess()
  {
    using var client = new HttpClient(new RefusalHandler()) { BaseAddress = new("https://api.test") };
    var model = new TopicDetailViewModel(new ApiTopicsService(new VouchaApiClient(client)));
    await model.LoadAsync("topic-1", TestContext.Current.CancellationToken);

    await model.ToggleTopicFollowAsync(TestContext.Current.CancellationToken);
    Assert.False(model.IsFollowingTopic);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpActionFailed), model.ErrorMessage);
    await model.ToggleSourceFollowAsync(TestContext.Current.CancellationToken);
    Assert.False(model.IsFollowingSource);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpActionFailed), model.ErrorMessage);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task NewsFollowRefusalsRestoreTheVisibleRow(bool topic)
  {
    var model = new NewsFeedsViewModel(new RefusingNewsService(), NewsFeedScope.AllSources);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var original = Assert.Single(model.Items);
    if (topic) await model.ToggleTopicFollowAsync(original, TestContext.Current.CancellationToken);
    else await model.ToggleSourceFollowAsync(original, TestContext.Current.CancellationToken);
    Assert.Equal(original, Assert.Single(model.Items));
    Assert.False(model.Items[0].IsFollowingSource);
    Assert.False(model.Items[0].IsFollowingTopic);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpActionFailed), model.ErrorMessage);
  }

  private sealed class RefusalHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
      if (request.Method != HttpMethod.Get)
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent(LimitBody) });
      var body = request.RequestUri!.AbsolutePath.Contains("rss-feeds", StringComparison.Ordinal)
          ? "{\"results\":[{\"id\":\"feed-1\",\"title\":\"Feed\",\"feed_type\":\"article\",\"url\":\"https://example.test/feed\"}],\"page_info\":{\"has_next_page\":false}}"
          : "{\"topic\":{\"id\":\"topic-1\",\"name\":\"Topic\",\"slug\":\"topic\",\"topic_type\":\"rss_feed\"}}";
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
  }

  private sealed class RefusingNewsService : INewsFeedService
  {
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NewsFeedItem>>([new("feed-1", "Feed", "News", "", null,
            DateTimeOffset.UnixEpoch, NewsFeedItemKind.Source, TopicId: "topic-1")]);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType, CancellationToken cancellationToken = default) =>
        GetNewsFeedItemsAsync(scope, cancellationToken);
    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) =>
        Task.FromException(new VouchaApiException(HttpStatusCode.Conflict, LimitBody));
    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) =>
        Task.FromException(new VouchaApiException(HttpStatusCode.Conflict, LimitBody));
    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}
