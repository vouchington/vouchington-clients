using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.Logging;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.ReferralLinks;
using Voucha.Client.Core.Search;

namespace Voucha.Client.App;

public sealed partial class AppShell : Shell
{
  private const string SessionRoute = "session";
  private const string CustomizeNavigationRoute = "customize-navigation";
  private const string LandingPagesRoute = "landing-pages";
  private const string LandingPagesInfoRoute = "landing-pages-info";
  private readonly IServiceProvider serviceProvider;
  private readonly INavigationViewerProvider viewerProvider;
  private readonly BottomTabPreferenceStore preferenceStore;
  private readonly OmnisearchWebSearchRouteContextStore webSearchRouteContextStore;
  private readonly FriendsRouteContextStore friendsRouteContextStore;
  private readonly ReferralLinksRouteContextStore referralLinksRouteContextStore;
  private readonly IUiLocalization localization;
  private readonly object pendingAuthenticatedUrlsGate = new();
  private readonly Queue<Uri> pendingAuthenticatedUrls = new();
  private readonly object pendingFeatureFlagUrlsGate = new();
  private readonly Queue<Uri> pendingFeatureFlagUrls = new();
  private NavigationViewer renderedViewer;
  private readonly object pendingIntentRouteMatchesGate = new();
  private readonly Dictionary<string, NativeRouteMatch> pendingIntentRouteMatches = new(StringComparer.Ordinal);

  public AppShell(
      IServiceProvider serviceProvider,
      INavigationViewerProvider viewerProvider,
      BottomTabPreferenceStore preferenceStore,
      OmnisearchWebSearchRouteContextStore webSearchRouteContextStore,
      FriendsRouteContextStore friendsRouteContextStore,
      ReferralLinksRouteContextStore referralLinksRouteContextStore,
      ILogger<AppShell> logger,
      ISessionStore sessionStore,
      IUiLocaleController localeController,
      IUiLocalization localization)
  {
    this.serviceProvider = serviceProvider;
    this.viewerProvider = viewerProvider;
    this.preferenceStore = preferenceStore;
    this.webSearchRouteContextStore = webSearchRouteContextStore;
    this.friendsRouteContextStore = friendsRouteContextStore;
    this.referralLinksRouteContextStore = referralLinksRouteContextStore;
    renderedViewer = viewerProvider.CurrentViewer;
    this.localization = localization;

    viewerProvider.ViewerChanged += OnViewerChanged;
    preferenceStore.PreferencesChanged += (_, _) =>
        MainThread.BeginInvokeOnMainThread(() => RebuildNavigation(viewerProvider.CurrentViewer));
    localeController.LocaleChanged += OnLocaleChanged;
    RebuildNavigation(viewerProvider.CurrentViewer);
    AppLinkDispatcher.Attach(HandleAppLinkAsync);
    _ = RefreshSessionOnStartupAsync(sessionStore, logger);
  }

  private static async Task RefreshSessionOnStartupAsync(
      ISessionStore sessionStore,
      ILogger<AppShell> logger)
  {
    try
    {
      await sessionStore.RefreshAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      LogStartupSessionRefreshFailed(logger, ex);
    }
  }

  [LoggerMessage(
      EventId = 1,
      Level = LogLevel.Warning,
      Message = "Startup session refresh failed.")]
  private static partial void LogStartupSessionRefreshFailed(
      ILogger logger,
      Exception exception);

  private void RebuildNavigation(NavigationViewer viewer)
  {
    renderedViewer = viewer;
    var navigationShell = BottomTabShellViewModel.Create(viewer, preferenceStore.Load(), localization);
    var tabBar = new TabBar();

    Items.Clear();
    Items.Add(new FlyoutItem
    {
      Title = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpSession),
      Route = SessionRoute,
      Items =
      {
        new ShellContent
        {
          Title = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpSession),
          Route = SessionRoute,
          ContentTemplate = new DataTemplate(() => serviceProvider.GetRequiredService<AuthPage>()),
        },
      },
    });
    Items.Add(new FlyoutItem
    {
      Title = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCustomizeNavigation),
      Route = CustomizeNavigationRoute,
      Items =
      {
        new ShellContent
        {
          Title = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCustomize),
          Route = CustomizeNavigationRoute,
          ContentTemplate = new DataTemplate(() => serviceProvider.GetRequiredService<BottomTabCustomizePage>()),
        },
      },
    });
    AddLandingPagesInfoRoute(viewer);

    foreach (var tab in navigationShell.Tabs)
    {
      tabBar.Items.Add(BottomTabFactory.Create(tab, () => CreateIntentPage(tab)));
    }

    Items.Add(tabBar);

    foreach (var intent in NavigationCatalog.GetVisibleNonBottomIntents(viewer)
                 .Select(intent => NavigationIntentViewModel.FromIntent(intent, viewer, localization)))
    {
      var flyoutItem = new FlyoutItem { Title = intent.Label };
      flyoutItem.Items.Add(new Tab
      {
        Title = intent.Label,
        Items =
        {
          new ShellContent
          {
            Title = intent.Label,
            Route = intent.Id,
            ContentTemplate = new DataTemplate(() => CreateIntentPage(intent)),
          },
        },
      });
      Items.Add(flyoutItem);
    }
  }

  public Task OpenSessionAsync() => OpenRouteAsync(SessionRoute);

}
