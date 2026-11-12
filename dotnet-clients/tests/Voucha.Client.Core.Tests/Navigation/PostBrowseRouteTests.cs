using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class PostBrowseRouteTests
{
  [Theory]
  [InlineData("/posts", null)]
  [InlineData("/reviews", "review")]
  [InlineData("/discussions", "discussion")]
  [InlineData("/data-points", "data_point")]
  [InlineData("/articles", "article")]
  [InlineData("/blog", "blog")]
  [InlineData("/links", "link")]
  public void GlobalBrowseRoutesKeepRequestedType(string path, string? type)
  {
    var resolution = NativeDeepLinkResolver.Resolve(path, new NavigationViewer(true, []));
    var scope = PostBrowseRoute.FromMatch(resolution.Match);
    Assert.NotNull(scope);
    Assert.Equal(type, scope.PostTypes);
    Assert.Null(scope.TimeRange);
    Assert.Null(scope.Topics);
  }

  [Theory]
  [InlineData("/feed/posts")]
  [InlineData("/feed/posts/friends")]
  [InlineData("/feed/posts/topics")]
  public void FeedRoutesRetainTheirOwnFeedSelection(string path)
  {
    var resolution = NativeDeepLinkResolver.Resolve(path, new NavigationViewer(true, []));
    Assert.Null(PostBrowseRoute.FromMatch(resolution.Match));
  }
}
