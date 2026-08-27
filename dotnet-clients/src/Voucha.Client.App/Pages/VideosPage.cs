using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public sealed class VideosPage : NewsFeedsPage
{
  public VideosPage(
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
              NewsFeedKind.Videos,
              initialScope ?? (sessionStore.Current.IsAuthenticated
                  ? NewsFeedKind.Videos.GetAuthenticatedScope()
                  : NewsFeedKind.Videos.GetAnonymousScope()),
              bookmarkService,
              localization,
              localeController),
          sessionStore,
          serviceProvider,
          emailRecovery)
  {
  }
}
