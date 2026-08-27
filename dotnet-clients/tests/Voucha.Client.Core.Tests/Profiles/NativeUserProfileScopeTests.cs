using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Profiles;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed class NativeUserProfileScopeTests
{
  public static TheoryData<string, NativeUserProfileSection, NativeUserProfileCollectionKind> SupportedRoutes => new()
  {
    { "/user/alice", NativeUserProfileSection.Overview, NativeUserProfileCollectionKind.None },
    { "/user/alice/posts", NativeUserProfileSection.Posts, NativeUserProfileCollectionKind.PostsAll },
    { "/user/alice/reviews", NativeUserProfileSection.Posts, NativeUserProfileCollectionKind.Reviews },
    { "/user/alice/discussions", NativeUserProfileSection.Posts, NativeUserProfileCollectionKind.Discussions },
    { "/user/alice/comments", NativeUserProfileSection.Posts, NativeUserProfileCollectionKind.Comments },
    { "/user/alice/topics/following", NativeUserProfileSection.Topics, NativeUserProfileCollectionKind.TopicsFollowing },
    { "/user/alice/users/following", NativeUserProfileSection.Friends, NativeUserProfileCollectionKind.UsersFollowing },
    { "/user/alice/users/followers", NativeUserProfileSection.Friends, NativeUserProfileCollectionKind.UsersFollowers },
    { "/user/alice/rss-feeds/following", NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesAll },
    { "/user/alice/rss-feeds/following?feed_type=article", NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesNews },
    { "/user/alice/rss-feeds/following?feed_type=podcast", NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesPodcasts },
    { "/user/alice/rss-feeds/following?feed_type=video", NativeUserProfileSection.Sources, NativeUserProfileCollectionKind.SourcesVideos },
    { "/user/alice/communities/member", NativeUserProfileSection.Communities, NativeUserProfileCollectionKind.CommunitiesMember },
  };

  [Theory]
  [MemberData(nameof(SupportedRoutes))]
  public void ParseRecognizesEverySupportedRoute(
      string route,
      NativeUserProfileSection section,
      NativeUserProfileCollectionKind collection)
  {
    var match = Match(route);

    Assert.True(NativeUserProfileScope.TryParse(match, out var scope));
    Assert.Equal(section, scope.Section);
    Assert.Equal(collection, scope.Collection);
  }

  [Theory]
  [InlineData("/user/alice/unknown")]
  [InlineData("/user/alice/rss-feeds/following?feed_type=music")]
  public void ParseRejectsUnsupportedRoutes(string route)
  {
    var match = new NativeRoutePattern("/user/:idOrUsername/**").Match(route)!;

    Assert.False(NativeUserProfileScope.TryParse(match, out _));
  }

  [Fact]
  public void ScopeTabRowResolvesSelectedAndUnselectedPresentation()
  {
    var selected = new ProfileScopeTabRow(
        NativeUserProfileSection.Friends,
        NativeUserProfileCollectionKind.UsersFollowers,
        UiText.Verbatim("Followers"),
        3,
        true,
        true);
    var unselected = selected with { IsSelected = false };

    Assert.Equal("Followers", selected.Label);
    Assert.NotNull(selected.SelectionText);
    Assert.Equal("Selected", selected.SelectionLabel);
    Assert.Equal("Followers, selected", selected.AccessibilityDescription);
    Assert.Null(unselected.SelectionText);
    Assert.Null(unselected.SelectionLabel);
    Assert.Equal("Followers", unselected.AccessibilityDescription);
  }

  private static NativeRouteMatch Match(string route) =>
      NativeRouteCatalog.MatchingRoute(route)!.Value.Match;
}
