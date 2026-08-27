using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class PodcastsPage : NewsFeedsPage
{
  public PodcastsPage(
      INewsFeedService newsFeedService,
      ISessionStore sessionStore,
      IBookmarkService bookmarkService,
      IServiceProvider serviceProvider,
      EmailVerificationRecoveryCoordinator emailRecovery,
      IUiLocalization localization,
      IUiLocaleController localeController,
      NewsFeedScope? initialScope = null)
      : base(
          new NewsFeedsViewModel(
              newsFeedService,
              NewsFeedKind.Podcasts,
              initialScope ?? (sessionStore.Current.IsAuthenticated
                  ? NewsFeedKind.Podcasts.GetAuthenticatedScope()
                  : NewsFeedKind.Podcasts.GetAnonymousScope()),
              bookmarkService,
              localization,
              localeController),
          sessionStore,
          serviceProvider,
          emailRecovery)
  {
  }
}
