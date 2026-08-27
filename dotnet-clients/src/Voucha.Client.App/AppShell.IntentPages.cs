using Voucha.Client.App.Pages;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private void AddLandingPagesInfoRoute(NavigationViewer viewer)
  {
    var intent = NavigationCatalog.All.Single(intent => intent.Id == LandingPagesRoute);
    var viewModel = NavigationIntentViewModel.FromIntent(intent, viewer, localization);
    Items.Add(new FlyoutItem
    {
      Title = viewModel.Label,
      Route = LandingPagesInfoRoute,
      FlyoutItemIsVisible = false,
      Items =
      {
        new ShellContent
        {
          Title = viewModel.Label,
          Route = LandingPagesInfoRoute,
          ContentTemplate = new DataTemplate(() => new NavigationIntentPage(viewModel)),
        },
      },
    });
  }

  private Page CreateIntentPage(NavigationIntentViewModel intent)
  {
    var match = ConsumePendingIntentRouteMatch(intent.Id);
    if (match is { Path: var path } && IsImportExportPath(path)) return CreateImportExportPage(path);

    return intent.Id switch
    {
      NavigationCatalog.NewsIntentId => serviceProvider.GetRequiredService<NewsFeedsPage>(),
      "podcasts" => CreatePodcastsPage(match),
      "videos" => CreateVideosPage(match),
      "web-search" => new OmnisearchPage(
          serviceProvider.GetRequiredService<OmnisearchViewModel>(),
          serviceProvider.GetRequiredService<ITurnstileTokenProvider>(),
          serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>(),
          intent),
      "fediverse" => CreateFediverseSearchPage(intent, match),
      NavigationCatalog.ListsIntentId => serviceProvider.GetRequiredService<ListsPage>(),
      NavigationCatalog.MessagesIntentId => CreateMessagesPage(match),
      "chat" => CreateChatPage(match),
      "crm" => CreateCrmPage(match, intent),
      "friends" => serviceProvider.GetRequiredService<FriendsPage>(),
      NavigationCatalog.SettingsIntentId => CreateSettingsPage(match),
      "posts" => serviceProvider.GetRequiredService<PostsPage>(),
      "topics" => serviceProvider.GetRequiredService<TopicsPage>(),
      "referral-links" => serviceProvider.GetRequiredService<ReferralLinksPage>(),
      "moderation" => CreateModerationPage(match),
      "engineering" => CreateEngineeringPage(match, intent),
      LandingPagesRoute => serviceProvider.GetRequiredService<LandingPagesPage>(),
      "growth" => CreateGrowthDashboardPage(match),
      _ => new NavigationIntentPage(intent),
    };
  }

  private PodcastsPage CreatePodcastsPage(NativeRouteMatch? match) =>
      new(
          serviceProvider.GetRequiredService<INewsFeedService>(),
          serviceProvider.GetRequiredService<ISessionStore>(),
          serviceProvider.GetRequiredService<IBookmarkService>(),
          serviceProvider,
          serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>(),
          serviceProvider.GetRequiredService<IUiLocalization>(),
          serviceProvider.GetRequiredService<IUiLocaleController>(),
          GetInitialMediaScope(NewsFeedKind.Podcasts, match));

  private VideosPage CreateVideosPage(NativeRouteMatch? match) =>
      new(
          serviceProvider.GetRequiredService<INewsFeedService>(),
          serviceProvider.GetRequiredService<ISessionStore>(),
          serviceProvider.GetRequiredService<IBookmarkService>(),
          serviceProvider,
          serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>(),
          serviceProvider.GetRequiredService<IUiLocalization>(),
          serviceProvider.GetRequiredService<IUiLocaleController>(),
          GetInitialMediaScope(NewsFeedKind.Videos, match));

  private NativeRouteMatch? ConsumePendingIntentRouteMatch(string intentId)
  {
    lock (pendingIntentRouteMatchesGate)
    {
      if (!pendingIntentRouteMatches.TryGetValue(intentId, out var match))
      {
        return null;
      }

      pendingIntentRouteMatches.Remove(intentId);
      return match;
    }
  }

  private static NewsFeedScope? GetInitialMediaScope(NewsFeedKind kind, NativeRouteMatch? match)
  {
    if (match is null)
    {
      return null;
    }

    return (kind, match.Path) switch
    {
      (NewsFeedKind.Podcasts, "/feed/podcasts") => NewsFeedScope.YourPodcasts,
      (NewsFeedKind.Podcasts, "/feed/podcasts/friends") => NewsFeedScope.YourPodcasts,
      (NewsFeedKind.Podcasts, "/feed/podcasts/sources") => NewsFeedScope.YourPodcastSources,
      (NewsFeedKind.Podcasts, "/feed/podcasts/topics") => NewsFeedScope.YourPodcasts,
      (NewsFeedKind.Podcasts, "/podcast-episodes") => NewsFeedScope.AllPodcasts,
      (NewsFeedKind.Podcasts, "/podcasts") => NewsFeedScope.AllPodcastSources,
      (NewsFeedKind.Podcasts, "/my/podcasts") => NewsFeedScope.YourPodcastSources,
      (NewsFeedKind.Videos, "/feed/videos") => NewsFeedScope.YourVideos,
      (NewsFeedKind.Videos, "/feed/videos/friends") => NewsFeedScope.YourVideos,
      (NewsFeedKind.Videos, "/feed/videos/sources") => NewsFeedScope.YourVideoSources,
      (NewsFeedKind.Videos, "/feed/videos/topics") => NewsFeedScope.YourVideos,
      (NewsFeedKind.Videos, "/videos") => NewsFeedScope.AllVideos,
      (NewsFeedKind.Videos, "/channels") => NewsFeedScope.AllVideoSources,
      (NewsFeedKind.Videos, "/my/channels") => NewsFeedScope.YourVideoSources,
      _ => null,
    };
  }

  private async Task<bool> ShouldStoreIntentRouteMatchAsync(string intentId, NativeRouteMatch? match)
  {
    if (match is null)
    {
      return false;
    }

    if (IsImportExportPath(match.Path)) return await OpenOrStoreImportExportRouteAsync(intentId, match.Path);

    if (intentId == NavigationCatalog.MessagesIntentId)
    {
      return await PrepareMessagesIntentRouteMatchAsync(match);
    }

    if (intentId == "growth")
    {
      return await PrepareGrowthIntentRouteMatchAsync(match);
    }

    if (intentId == "moderation")
    {
      return await PrepareModerationIntentRouteMatchAsync(match).ConfigureAwait(true);
    }

    if (intentId == "crm")
    {
      return await PrepareCrmIntentRouteMatchAsync(match).ConfigureAwait(true);
    }

    if (intentId == "engineering")
    {
      return await PrepareEngineeringIntentRouteMatchAsync(match).ConfigureAwait(true);
    }

    if (intentId == "fediverse")
    {
      return await PrepareFediverseIntentRouteMatchAsync(match).ConfigureAwait(true);
    }

    if (intentId == NavigationCatalog.SettingsIntentId) return await TryApplySettingsRouteMatchAsync(match).ConfigureAwait(true);

    var kind = intentId switch
    {
      "podcasts" => NewsFeedKind.Podcasts,
      "videos" => NewsFeedKind.Videos,
      _ => (NewsFeedKind?)null,
    };
    if (kind is null)
    {
      return intentId != "chat" || !await TryApplyChatRouteMatchAsync(match);
    }

    if (GetInitialMediaScope(kind.Value, match) is not { } scope)
    {
      return false;
    }

    foreach (var page in EnumerateShellContentPages(intentId))
    {
      if (page is NewsFeedsPage feedsPage)
      {
        await feedsPage.SelectScopeAsync(scope);
        return false;
      }
    }

    return true;
  }

}
