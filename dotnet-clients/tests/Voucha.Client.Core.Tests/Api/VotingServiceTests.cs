using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VotingServiceTests
{
  [Fact]
  public async Task ApiPostsServiceVotesThroughThePostRoute()
  {
    var (service, handler) = CreatePostsService();

    await service.VotePostAsync("post-1", ElectionVoteChoice.Vouch, TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Put, handler.Method);
    Assert.Equal("/api/v1/posts/post-1/vote", handler.PathAndQuery);
    Assert.Contains("\"choice\":\"vouch\"", handler.RequestBody!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ApiTopicsServiceVotesThroughTheTopicRoute()
  {
    var (service, handler) = CreateTopicsService();

    await service.VoteTopicAsync("topic-1", ElectionVoteChoice.Disavow, TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Put, handler.Method);
    Assert.Equal("/api/v1/topics/topic-1/vote", handler.PathAndQuery);
    Assert.Contains("\"choice\":\"disavow\"", handler.RequestBody!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ApiNewsFeedServiceVotesThroughTheRssFeedItemRoute()
  {
    var (service, handler) = CreateNewsFeedService();

    await service.VoteRssFeedItemAsync("item-1", ElectionVoteChoice.Neutral, TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Put, handler.Method);
    Assert.Equal("/api/v1/rss-feed-items/item-1/vote", handler.PathAndQuery);
    Assert.Contains("\"choice\":\"neutral\"", handler.RequestBody!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ClearingAnElectionUsesDeleteWithoutABody()
  {
    var (service, handler) = CreatePostsService();

    await service.ClearPostVoteAsync("post-1", TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Delete, handler.Method);
    Assert.Equal("/api/v1/posts/post-1/vote", handler.PathAndQuery);
    Assert.Null(handler.RequestBody);
  }

  [Fact]
  public async Task UserTrustUsesSemanticSentimentAndASeparateClearRoute()
  {
    var handler = new RecordingHandler(string.Empty, HttpStatusCode.NoContent);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.VoteUserTrustAsync("user-1", ElectionVoteChoice.Disavow, TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Put, handler.Method);
    Assert.Equal("/api/v1/users/user-1/vouch-vote", handler.PathAndQuery);
    Assert.Contains("\"choice\":\"disavow\"", handler.RequestBody!, StringComparison.Ordinal);

    await client.ClearUserTrustVoteAsync("user-1", TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Delete, handler.Method);
    Assert.Equal("/api/v1/users/user-1/vouch-vote", handler.PathAndQuery);
    Assert.Null(handler.RequestBody);
  }

  private static (ApiPostsService Service, RecordingHandler Handler) CreatePostsService()
  {
    var handler = new RecordingHandler(string.Empty, HttpStatusCode.NoContent);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiPostsService(client), handler);
  }

  private static (ApiTopicsService Service, RecordingHandler Handler) CreateTopicsService()
  {
    var handler = new RecordingHandler(string.Empty, HttpStatusCode.NoContent);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiTopicsService(client), handler);
  }

  private static (ApiNewsFeedService Service, RecordingHandler Handler) CreateNewsFeedService()
  {
    var handler = new RecordingHandler(string.Empty, HttpStatusCode.NoContent);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiNewsFeedService(client), handler);
  }
}
