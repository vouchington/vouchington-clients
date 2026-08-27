using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

public static class NewsFeedScopeExtensions
{
  public static NewsFeedKind GetKind(this NewsFeedScope scope) =>
      scope switch
      {
        NewsFeedScope.YourFeed or NewsFeedScope.AllNews or NewsFeedScope.YourSources or NewsFeedScope.AllSources => NewsFeedKind.News,
        NewsFeedScope.YourPodcasts or NewsFeedScope.AllPodcasts or NewsFeedScope.YourPodcastSources or NewsFeedScope.AllPodcastSources => NewsFeedKind.Podcasts,
        NewsFeedScope.YourVideos or NewsFeedScope.AllVideos or NewsFeedScope.YourVideoSources or NewsFeedScope.AllVideoSources => NewsFeedKind.Videos,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown news feed scope."),
      };

  public static bool IsSourcesScope(this NewsFeedScope scope) =>
      scope is NewsFeedScope.YourSources
          or NewsFeedScope.AllSources
          or NewsFeedScope.YourPodcastSources
          or NewsFeedScope.AllPodcastSources
          or NewsFeedScope.YourVideoSources
          or NewsFeedScope.AllVideoSources;

  public static string GetFeedType(this NewsFeedScope scope) =>
      scope.GetKind() switch
      {
        NewsFeedKind.News => "article",
        NewsFeedKind.Podcasts => "podcast",
        NewsFeedKind.Videos => "video",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown news feed scope."),
      };

  public static NewsFeedSourceType GetDefaultSourceFeedType(this NewsFeedKind kind) =>
      kind switch
      {
        NewsFeedKind.News => NewsFeedSourceType.Article,
        NewsFeedKind.Podcasts => NewsFeedSourceType.Podcast,
        NewsFeedKind.Videos => NewsFeedSourceType.Video,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static NewsFeedSourceType GetDefaultSourceFeedType(this NewsFeedScope scope) =>
      scope.GetKind().GetDefaultSourceFeedType();

  public static string GetMediaType(this NewsFeedScope scope) =>
      scope.GetKind() switch
      {
        NewsFeedKind.News => "article",
        NewsFeedKind.Podcasts => "audio",
        NewsFeedKind.Videos => "video",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown news feed scope."),
      };

  public static NewsFeedScope GetAuthenticatedScope(this NewsFeedKind kind) =>
      kind switch
      {
        NewsFeedKind.News => NewsFeedScope.YourFeed,
        NewsFeedKind.Podcasts => NewsFeedScope.YourPodcasts,
        NewsFeedKind.Videos => NewsFeedScope.YourVideos,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static NewsFeedScope GetAnonymousScope(this NewsFeedKind kind) =>
      kind switch
      {
        NewsFeedKind.News => NewsFeedScope.AllNews,
        NewsFeedKind.Podcasts => NewsFeedScope.AllPodcasts,
        NewsFeedKind.Videos => NewsFeedScope.AllVideos,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static NewsFeedScope GetSourcesScope(this NewsFeedKind kind) =>
      kind switch
      {
        NewsFeedKind.News => NewsFeedScope.YourSources,
        NewsFeedKind.Podcasts => NewsFeedScope.YourPodcastSources,
        NewsFeedKind.Videos => NewsFeedScope.YourVideoSources,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static NewsFeedScope GetAllSourcesScope(this NewsFeedKind kind) =>
      kind switch
      {
        NewsFeedKind.News => NewsFeedScope.AllSources,
        NewsFeedKind.Podcasts => NewsFeedScope.AllPodcastSources,
        NewsFeedKind.Videos => NewsFeedScope.AllVideoSources,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static string GetPageTitle(this NewsFeedKind kind, IUiLocalization? localization = null) =>
      kind switch
      {
        NewsFeedKind.News => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsNews),
        NewsFeedKind.Podcasts => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsPodcasts),
        NewsFeedKind.Videos => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsVideos),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static string GetPrimaryScopeLabel(this NewsFeedKind kind, IUiLocalization? localization = null) =>
      kind switch
      {
        NewsFeedKind.News => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsYourFeed),
        NewsFeedKind.Podcasts => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsYourPodcasts),
        NewsFeedKind.Videos => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsYourVideos),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static string GetAllItemsScopeLabel(this NewsFeedKind kind, IUiLocalization? localization = null) =>
      kind switch
      {
        NewsFeedKind.News => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsAllNews),
        NewsFeedKind.Podcasts => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsAllPodcasts),
        NewsFeedKind.Videos => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsAllVideos),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static string GetSourcesScopeLabel(this NewsFeedKind kind, IUiLocalization? localization = null) =>
      kind switch
      {
        NewsFeedKind.News or NewsFeedKind.Podcasts or NewsFeedKind.Videos =>
          L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsYourSources),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  public static string GetAllSourcesScopeLabel(this NewsFeedKind kind, IUiLocalization? localization = null) =>
      kind switch
      {
        NewsFeedKind.News or NewsFeedKind.Podcasts or NewsFeedKind.Videos =>
          L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsAllSources),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown news feed kind."),
      };

  private static IUiLocalization L(IUiLocalization? localization) => localization ?? UiLocalization.English;
}
