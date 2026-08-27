using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed class ProfileNavigationTargetsTests
{
  [Theory]
  [InlineData("topic", "topic slug", "/topic/topic%20slug")]
  [InlineData("rss_feed", "source/slug", "/source/source%2Fslug")]
  [InlineData("fediverse_instance", "instance slug", "/instance/instance%20slug")]
  [InlineData("referral_program", "program", "/referral-program/program")]
  [InlineData("topic", null, "/topic/topic-1")]
  public void TopicTargetsUseSharedCanonicalMapping(
      string topicType,
      string? slug,
      string destination)
  {
    var row = new TopicRow(
        "topic-1",
        "Topic",
        slug!,
        topicType,
        UiTaxonomy.TopicType(topicType),
        null);

    Assert.Equal(destination, ProfileNavigationTargets.Topic(row));
  }

  [Theory]
  [InlineData("topic slug", "topic-id", "feed-id", "/source/topic%20slug")]
  [InlineData(null, "topic/id", "feed-id", "/source/topic%2Fid")]
  [InlineData(null, null, "feed id", "/source/feed%20id")]
  public void SourceTargetsUseSharedCanonicalMapping(
      string? topicSlug,
      string? topicId,
      string feedId,
      string destination)
  {
    var topic = topicId is null ? null : new Topic(topicId, "Source topic", topicSlug!, "rss_feed");
    var row = new RssFeedSource(
        feedId, "Source", null, null, null, null, topic, null, null, null);

    Assert.Equal(destination, ProfileNavigationTargets.Source(row));
  }

  [Fact]
  public void EveryScopedRowTargetUsesACanonicalNativeRoute()
  {
    var targets = new (string Path, NativeRouteDestinationId Destination)[]
    {
      (ProfileNavigationTargets.User(new FriendRow("user-1", "Alice", null, "alice", null, false, false)), NativeRouteDestinationId.UserProfile),
      (ProfileNavigationTargets.Topic(new TopicRow(
          "topic-1",
          "Program",
          "program",
          "referral_program",
          UiTaxonomy.TopicType("referral_program"),
          null)), NativeRouteDestinationId.TopicDetail),
      (ProfileNavigationTargets.Source(new RssFeedSource("source-1", "Source", null, null, null, null, null, null, null, null)), NativeRouteDestinationId.SourceDetail),
      (ProfileNavigationTargets.Community(new CommunityBrowseRow("community-1", "Voucha", "voucha", 0, 0, false)), NativeRouteDestinationId.CommunityDetail),
      (ProfileNavigationTargets.SignIn, NativeRouteDestinationId.SignIn),
    };

    foreach (var (path, destination) in targets)
    {
      var match = NativeRouteCatalog.MatchingRoute(path);
      Assert.True(match.HasValue);
      Assert.Equal(destination, match.Value.Entry.DestinationId);
    }
  }
}
