using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Tags;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class BookmarkAndTagRouteTests
{
  [Theory]
  [InlineData("/my/news-sources", "article")]
  [InlineData("/my/podcasts", "podcast")]
  [InlineData("/my/channels", "video")]
  public void FollowedSourceRootsResolveAsBookmarkCollectionsBeforeFeedFamilies(string path, string feedType)
  {
    Assert.True(BookmarkCollectionRoutes.TryResolve(path, out var context));
    Assert.Equal(BookmarkCollectionKind.RssFeeds, context.Kind);
    Assert.Equal("following", context.ListType);
    Assert.Equal(feedType, context.FeedType);
    Assert.Equal(BookmarkPredicate.Follow, context.InverseAction?.Predicate);
    Assert.Equal("rss_feed", context.InverseAction?.EntityType);
  }

  [Theory]
  [InlineData("/my/news-items/saved", BookmarkCollectionKind.RssFeedItems, "saved", "article", null)]
  [InlineData("/my/podcasts/viewed", BookmarkCollectionKind.RssFeeds, "viewed", null, "podcast")]
  [InlineData("/my/friend-recommendations/dismissed", BookmarkCollectionKind.Users, "dismissed-recommendations", null, null)]
  [InlineData("/my/communities/proxy-following", BookmarkCollectionKind.Communities, "proxy-following", null, null)]
  public void BookmarkRoutesPreserveCollectionContext(
      string path,
      BookmarkCollectionKind kind,
      string listType,
      string? mediaType,
      string? feedType)
  {
    Assert.True(BookmarkCollectionRoutes.TryResolve(path, out var context));

    Assert.Equal(kind, context.Kind);
    Assert.Equal(listType, context.ListType);
    Assert.Equal(mediaType, context.MediaType);
    Assert.Equal(feedType, context.FeedType);
  }

  [Theory]
  [InlineData("/my/news-sources/import-export")]
  [InlineData("/my/podcasts/import-export")]
  [InlineData("/my/channels/import-export")]
  [InlineData("/my/sources/import-export")]
  public void ImportExportRoutesAreNotBookmarkCollections(string path)
  {
    Assert.False(BookmarkCollectionRoutes.TryResolve(path, out _));
  }

  [Theory]
  [InlineData("/discussion/post-1/tags/topic", "post", "post-1", "topic")]
  [InlineData("/source/source-1/tags/publisher_type", "topic", "source-1", "publisher_type")]
  [InlineData("/rss-feed-items/item-1/tags/topic", "rss_feed_item", "item-1", "topic")]
  public void TagRoutesPreserveSubjectContext(string path, string entityType, string entityId, string objectType)
  {
    Assert.True(TagManagementRoutes.TryResolve(path, out var context));

    Assert.Equal(entityType, context.EntityType);
    Assert.Equal(entityId, context.EntityIdOrSlug);
    Assert.Equal(objectType, context.ObjectType);
  }

  [Fact]
  public void TopicTagTabsHidePublisherTypeForNonSources()
  {
    Assert.DoesNotContain(TagRelationConfigs.GetTopicTagTabsForTopicType("topic"), tab => tab.Value == "publisher_type");
    Assert.Contains(TagRelationConfigs.GetTopicTagTabsForTopicType("rss_feed"), tab => tab.Value == "publisher_type");
  }

  [Fact]
  public void SupportedTagEntityTypesDescribeTheNativeTabs()
  {
    Assert.True(TagRelationConfigs.IsTopicTagSegment("publisher_type"));
    Assert.Contains(TagRelationConfigs.SupportedEntityTypes, entry => entry.EntityType == "post");
    Assert.Contains(TagRelationConfigs.SupportedEntityTypes, entry => entry.EntityType == "rss_feed_item");
    Assert.Single(TagRelationConfigs.SupportedEntityTypes.Single(entry => entry.EntityType == "rss_feed_item").Tabs);
  }
}
