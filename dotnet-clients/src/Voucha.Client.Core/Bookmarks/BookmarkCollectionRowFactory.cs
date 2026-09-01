using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Content;

namespace Voucha.Client.Core.Bookmarks;

internal static partial class BookmarkCollectionRowFactory
{
  public static BookmarkCollectionRow Post(
      Post post,
      BookmarkInverseAction? action,
      int rank,
      UrlEmbed? embed = null,
      IUiLocalization? localization = null)
  {
    var ui = localization ?? UiLocalization.English;
    var isComment = string.Equals(post.PostType, "comment", StringComparison.Ordinal);
    var slug = post.Slug ?? post.Id;
    var path = isComment ? null : DestinationForPost(post);
    return new(
        post.Id,
        "post",
        string.IsNullOrWhiteSpace(post.Title)
            ? UntitledPost(post.PostType)
            : UiText.UserContent(post.Title),
        ui,
        UiTaxonomy.PostType(post.PostType),
        string.IsNullOrWhiteSpace(post.Slug) ? null : UiText.UserContent(post.Slug),
        path,
        action,
        rank,
        isComment ? post.RootId ?? post.ParentId : null,
        EmbedPreview: UrlEmbedPreviews.From(embed));
  }

  public static BookmarkCollectionRow Topic(
      Topic topic,
      BookmarkInverseAction? action,
      int rank,
      IUiLocalization? localization = null) =>
      new(
          topic.Id,
          "topic",
          UiText.UserContent(topic.Name),
          localization ?? UiLocalization.English,
          UiTaxonomy.TopicType(topic.TopicType),
          null,
          NativeEntityDetailPaths.Topic(topic.Id, topic.Slug, topic.TopicType),
          action,
          rank);

  public static BookmarkCollectionRow User(
      User user,
      BookmarkInverseAction? action,
      int rank,
      IUiLocalization? localization = null) =>
      new(
          user.Id,
          "user",
          UserTitle(user),
          localization ?? UiLocalization.English,
          user.Username == user.Id || string.IsNullOrWhiteSpace(user.Username)
              ? null
              : UiText.UserContent(user.Username),
          null,
          $"/user/{Uri.EscapeDataString(user.Username ?? user.Id)}",
          action,
          rank);

  public static BookmarkCollectionRow RssItem(
      RssFeedItem item,
      BookmarkInverseAction? action,
      int rank,
      UrlEmbed? embed = null,
      IUiLocalization? localization = null)
  {
    var root = item.MediaType switch
    {
      "audio" => "/podcast-episodes",
      "video" => "/videos",
      _ => "/news",
    };
    return new(
        item.Id,
        "rss_feed_item",
        string.IsNullOrWhiteSpace(item.Title ?? item.Data?.Title)
            ? UiText.Localized(UiMessageKey.NativeDotnetNewsFeedsUntitled)
            : UiText.ExternalContent(item.Title ?? item.Data?.Title),
        localization ?? UiLocalization.English,
        UiTaxonomy.MediaType(item.MediaType),
        string.IsNullOrWhiteSpace(item.RssFeed?.Title)
            ? null
            : UiText.ExternalContent(item.RssFeed.Title),
        $"{root}?rss_item={Uri.EscapeDataString(item.Id)}",
        action,
        rank,
        EmbedPreview: UrlEmbedPreviews.From(embed));
  }

  public static BookmarkCollectionRow RssFeed(
      RssFeedSource feed,
      BookmarkInverseAction? action,
      int rank,
      IUiLocalization? localization = null) =>
      new(
          feed.Id,
          "rss_feed",
          string.IsNullOrWhiteSpace(feed.Title)
              ? UiText.Localized(UiMessageKey.NativeDotnetNewsFeedsUntitled)
              : UiText.ExternalContent(feed.Title),
          localization ?? UiLocalization.English,
          UiTaxonomy.NewsFeedSourceType(feed.FeedType?.ApiValue()),
          string.IsNullOrWhiteSpace(feed.Topic?.Name)
              ? null
              : UiText.UserContent(feed.Topic.Name),
          NativeEntityDetailPaths.Source(feed.Id, feed.Topic?.Id, feed.Topic?.Slug),
          action,
          rank);

  public static BookmarkCollectionRow Url(
      Url url,
      BookmarkInverseAction? action,
      int rank,
      IUiLocalization? localization = null) =>
      new(
          url.Id,
          "url",
          UiText.ExternalContent(url.UrlValue),
          localization ?? UiLocalization.English,
          UiText.ExternalContent(url.Hostname?.HostnameValue),
          UiText.ExternalContent(url.Pathname),
          $"/url/{Uri.EscapeDataString(url.Id)}",
          action,
          rank);

  public static BookmarkCollectionRow Hostname(
      Hostname hostname,
      BookmarkInverseAction? action,
      int rank,
      IUiLocalization? localization = null) =>
      new(
          hostname.Id,
          "hostname",
          UiText.ExternalContent(hostname.HostnameValue),
          localization ?? UiLocalization.English,
          UiText.Localized(UiMessageKey.NativeSwiftHouseholdsBookmarksDomain),
          null,
          $"/domain/{Uri.EscapeDataString(hostname.HostnameValue)}",
          action,
          rank);

  public static BookmarkCollectionRow Community(
      Community community,
      BookmarkInverseAction? action,
      int rank,
      IUiLocalization? localization = null) =>
      new(
          community.Id,
          "community",
          UiText.UserContent(community.Name),
          localization ?? UiLocalization.English,
          UiTaxonomy.ListVisibility(community.Visibility),
          null,
          $"/communities/{Uri.EscapeDataString(community.Slug)}",
          action,
          rank);

  private static UiText UntitledPost(string? postType) =>
      UiText.Localized(
          UiMessageKey.NativeDotnetPostsUntitledPostType,
          ("type", UiTaxonomy.PostType(postType)));

  private static UiText UserTitle(User user) =>
      !string.IsNullOrWhiteSpace(user.Name)
          ? UiText.UserContent(user.Name)
          : user.Username != user.Id && !string.IsNullOrWhiteSpace(user.Username)
              ? UiText.UserContent(user.Username)
              : UiText.Localized(UiMessageKey.NativeSwiftHouseholdsBookmarksUser);
}
