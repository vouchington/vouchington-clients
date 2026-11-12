using System.Reflection;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class TopicPostsRouteTests
{
  [Theory]
  [InlineData("/topic/travel/posts", "topics", null)]
  [InlineData("/topic/travel/discussions", "topics", null)]
  [InlineData("/topic/travel/reviews", "review_topic", "review")]
  [InlineData("/card/travel/data-points", "data_point_topic", "data_point")]
  [InlineData("/source/travel/reviews", "review_topic", "review")]
  [InlineData("/instance/travel/posts", "topics", null)]
  [InlineData("/bank-account/travel/posts", "topics", null)]
  [InlineData("/referral-program/travel/reviews", "review_topic", "review")]
  [InlineData("/rewards-program/travel/data-points", "data_point_topic", "data_point")]
  [InlineData("/rewards-program-status/travel/posts", "topics", null)]
  public async Task RegisteredTopicPostRoutesResolveCanonicalIdAndSerializeExactProducerScope(
      string path, string filter, string? postType)
  {
    var resolution = NativeDeepLinkResolver.Resolve(path, new NavigationViewer(true, []));
    var route = Assert.IsType<TopicPostsRoute>(TopicPostsRoute.FromResolution(resolution));
    var topics = DispatchProxy.Create<ITopicsService, TopicLookupProxy>();
    var scope = await route.ResolveRequestAsync(topics, TestContext.Current.CancellationToken);
    var request = VouchaApiEndpoints.AllPosts(scope);

    Assert.Equal("travel", ((TopicLookupProxy)(object)topics).Identifier);
    Assert.Equal("/api/v1/posts", request.Path);
    Assert.Equal("canonical-topic", request.Query[filter]);
    Assert.Equal(postType, scope.PostTypes);
    Assert.Equal("1m", request.Query["time_range"]);
    Assert.False(request.Query.ContainsKey("data_point_vertical"));
    Assert.Single(request.Query.Keys.Where(key => key is "topics" or "review_topic" or "data_point_topic"));
  }

  [Theory]
  [InlineData("/topic/travel")]
  [InlineData("/source/travel/crawls")]
  [InlineData("/instance/travel")]
  [InlineData("/topic/travel/news")]
  [InlineData("/user/alice/posts")]
  [InlineData("/communities/travel/posts")]
  public void OtherRegisteredSurfacesKeepTheirOwnDestination(string path)
  {
    var resolution = NativeDeepLinkResolver.Resolve(path, new NavigationViewer(true, []));
    Assert.Null(TopicPostsRoute.FromResolution(resolution));
  }

  public class TopicLookupProxy : DispatchProxy
  {
    public string? Identifier { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      if (targetMethod?.Name != nameof(ITopicsService.FetchTopicAsync)) throw new NotSupportedException();
      Identifier = (string)args![0]!;
      return Task.FromResult(new TopicResponse(new Topic("canonical-topic", "Travel", "travel", "topic")));
    }
  }
}
