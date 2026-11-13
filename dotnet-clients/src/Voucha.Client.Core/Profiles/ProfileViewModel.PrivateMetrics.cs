using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private static readonly (string Key, string Route)[] PrivateMetricRoutes =
  [
    ("posts_saved", "/my/posts/saved"),
    ("posts_hidden", "/my/posts/hidden"),
    ("posts_following", "/my/posts/following"),
    ("posts_subscribed", "/my/posts/subscribed"),
    ("users_muted", "/my/users/muted"),
    ("users_blocked", "/my/users/blocked"),
    ("users_dismissed_recommendations", "/my/users/dismissed-recommendations"),
    ("users_subscribed_posts", "/my/users/subscribed-posts"),
    ("topics_muted", "/my/topics/muted"),
    ("topics_blocked", "/my/topics/blocked"),
    ("topics_viewed", "/my/topics/viewed"),
    ("topics_dismissed_recommendations", "/my/topics/dismissed-recommendations"),
    ("domains_muted", "/my/domains/muted"),
    ("domains_blocked", "/my/domains/blocked"),
    ("urls_saved", "/my/urls/saved"),
    ("communities_saved", "/my/communities/saved"),
    ("communities_proxy_following", "/my/communities/proxy-following"),
    ("communities_proxy_muted", "/my/communities/proxy-muted"),
  ];

  private static readonly (string Key, UiMessageKey Label)[] AggregatePrivateMetricLabels =
  [
    ("rss_feed_items_hidden", UiMessageKey.ExtractedUsersUserRssFeedItemRoutesHiddenRssItems28d66515),
    ("rss_feed_items_saved", UiMessageKey.NativeDotnetBookmarksSavedRssItems),
    ("rss_feed_items_viewed", UiMessageKey.NativeDotnetBookmarksViewedRssItems),
    ("rss_feeds_subscribed", UiMessageKey.NativeDotnetBookmarksSubscribedRssFeeds),
    ("rss_feeds_muted", UiMessageKey.NativeDotnetBookmarksMutedRssFeeds),
    ("rss_feeds_viewed", UiMessageKey.NativeDotnetBookmarksViewedRssFeeds),
    ("topics_subscribed_posts", UiMessageKey.NativeDotnetBookmarksSubscribedTopicsForPosts),
    ("topics_subscribed_news", UiMessageKey.NativeDotnetBookmarksSubscribedTopicsForNews),
  ];

  public IReadOnlyList<ProfilePrivateMetricRow> PrivateMetricRows
  {
    get
    {
      var metrics = userMetrics;
      if (!CanEdit || Identity is null || currentViewerUserId != Identity.Id || User?.Id != Identity.Id || !IsOverviewScope ||
          metrics?.PrivateCount is not { } counts) return [];
      var routeLabels = PrivateMetricRoutes.Select(entry =>
      {
        if (!BookmarkCollectionRoutes.TryResolve(entry.Route, out var route))
          throw new InvalidOperationException();
        return (entry.Key, Label: route.TitleText);
      });
      var aggregateLabels = AggregatePrivateMetricLabels.Select(entry => (entry.Key, Label: UiText.Localized(entry.Label)));
      return routeLabels.Concat(aggregateLabels).Where(entry => counts.ContainsKey(entry.Key)).Select(entry =>
          new ProfilePrivateMetricRow(entry.Key, entry.Label, counts[entry.Key],
              metrics.PrivateCountCapped?.GetValueOrDefault(entry.Key) == true, localization)).ToArray();
    }
  }

  public bool HasPrivateMetrics => PrivateMetricRows.Count > 0;

  private void NotifyPrivateMetrics()
  {
    OnPropertyChanged(nameof(PrivateMetricRows));
    OnPropertyChanged(nameof(HasPrivateMetrics));
  }

}

public sealed record ProfilePrivateMetricRow(
    string Key, UiText LabelText, int Count, bool IsCapped, IUiLocalization? Localization = null)
{
  private IUiLocalization UiLocalization => Localization ?? Voucha.Client.Core.Localization.UiLocalization.English;
  public string Label => UiLocalization.Resolve(LabelText);
  public string CountLabel => ProfileMetricCount.Format(Count, IsCapped, UiLocalization);
}

internal static class ProfileMetricCount
{
  public static string Format(int count, bool capped, IUiLocalization localization) =>
      capped ? $"{localization.FormatNumber(count)}+" : localization.FormatNumber(count);
}
