using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Api;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  public Task OpenNativePathAsync(NativeRoutePath targetPath) => OpenEncodedNativePathAsync(targetPath.Value);

  public Task OpenNativePathAsync(string targetPath)
  {
    if (string.IsNullOrWhiteSpace(targetPath)) return Task.CompletedTask;
    if (Uri.TryCreate(targetPath, UriKind.Absolute, out var absoluteUrl))
    {
      return HandleAppLinkAsync(absoluteUrl);
    }

    var normalizedTarget = targetPath.Trim().TrimStart('/');
    return OpenEscapedNativePathAsync(normalizedTarget);
  }

  private Task OpenEncodedNativePathAsync(string targetPath) =>
      OpenNativeUriAsync(targetPath.Trim().TrimStart('/'));

  private Task OpenEscapedNativePathAsync(string normalizedTarget)
  {
    if (string.IsNullOrEmpty(normalizedTarget)) return Task.CompletedTask;
    var queryStart = normalizedTarget.IndexOf('?', StringComparison.Ordinal);
    var normalizedPath = queryStart >= 0 ? normalizedTarget[..queryStart] : normalizedTarget;
    var query = queryStart >= 0 ? normalizedTarget[queryStart..] : string.Empty;
    var escapedPath = string.Join(
        '/',
        normalizedPath.Split('/', StringSplitOptions.None).Select(Uri.EscapeDataString));
    return OpenNativeUriAsync($"{escapedPath}{query}");
  }

  private Task OpenNativeUriAsync(string target)
  {
    return Uri.TryCreate($"voucha://{target}", UriKind.Absolute, out var appLink)
        ? HandleAppLinkAsync(appLink)
        : Task.CompletedTask;
  }

  private Task HandleAppLinkAsync(Uri url)
  {
    if (url.Scheme.Equals("voucha", StringComparison.OrdinalIgnoreCase) &&
        url.Host.Equals("auth", StringComparison.OrdinalIgnoreCase) &&
        url.AbsolutePath == "/oauth/callback")
    {
      return HandleOAuthCallbackAsync(url);
    }

    if (url.Scheme.Equals("voucha", StringComparison.OrdinalIgnoreCase) &&
        url.Host == "auth" && url.AbsolutePath == "/bluesky/callback")
    {
      return HandleBlueskyCallbackAsync(url);
    }

    if (serviceProvider.GetRequiredService<AppleSignInCallbackStore>().TryComplete(url))
    {
      return Task.CompletedTask;
    }

    var resolution = NativeDeepLinkResolver.Resolve(url, viewerProvider.CurrentViewer);
    if (resolution.ShouldShowSignIn)
    {
      serviceProvider.GetRequiredService<AuthLinkPrefillStore>().Set(new AuthLinkPrefill(
          resolution.Match?.QueryValue("emailAddress"),
          resolution.Match?.QueryValue("otp")));
      return OpenSessionAsync();
    }

    if (resolution.ShouldQueueUntilAuthenticated)
    {
      lock (pendingAuthenticatedUrlsGate) pendingAuthenticatedUrls.Enqueue(url);
      return OpenSessionAsync();
    }

    if (ShouldQueueUntilFeatureFlagsHydrate(resolution))
    {
      lock (pendingFeatureFlagUrlsGate) pendingFeatureFlagUrls.Enqueue(url);
      return Task.CompletedTask;
    }

    if (!resolution.CanNavigate || resolution.IntentId is not { } intentId)
    {
      return Task.CompletedTask;
    }

    if (TryOpenHouseholdRouteAsync(resolution) is { } householdRoute)
    {
      return householdRoute;
    }
    if (TryOpenPaymentCardsRouteAsync(resolution) is { } paymentCardsRoute)
    {
      return paymentCardsRoute;
    }
    if (TryOpenPointValuationsRouteAsync(resolution) is { } pointValuationsRoute)
    {
      return pointValuationsRoute;
    }
    if (TryOpenSpendingCategoriesRouteAsync(resolution) is { } spendingCategoriesRoute)
    {
      return spendingCategoriesRoute;
    }

    if (TryOpenRewardsProgramStatusesRouteAsync(resolution) is { } rewardsProgramStatusesRoute)
    {
      return rewardsProgramStatusesRoute;
    }

    SetWebSearchRouteContext(resolution);
    SetReferralLinksRouteContext(resolution);
    if (TryOpenFocusedRssFeedItemRouteAsync(resolution) is { } focusedItemRoute)
    {
      return focusedItemRoute;
    }
    if (TryOpenUsersBrowseRouteAsync(resolution) is { } usersBrowseRoute)
    {
      return usersBrowseRoute;
    }
    if (TryOpenFriendsRouteAsync(resolution) is { } friendsRoute)
    {
      return friendsRoute;
    }

    if (intentId == LandingPagesRoute)
    {
      return LandingPagesDeepLinkPolicy.TryGetOwnerManagementSlug(resolution, out var slug)
          ? OpenIntentAsync(intentId, slug)
          : OpenRouteAsync(LandingPagesInfoRoute);
    }

    if (TryOpenBookmarkRouteAsync(resolution) is { } bookmarkRoute)
    {
      return bookmarkRoute;
    }

    if (TryOpenTagManagementRouteAsync(resolution) is { } tagRoute)
    {
      return tagRoute;
    }

    if (TryOpenPostRouteAsync(resolution) is { } postRoute)
    {
      return postRoute;
    }

    var directRoute = TryOpenTopicRouteAsync(resolution)
        ?? TryOpenCommunityRouteAsync(resolution)
        ?? TryOpenUserAdminRouteAsync(resolution)
        ?? TryOpenUserProfileRouteAsync(resolution);
    if (directRoute is not null) return directRoute;

    return OpenIntentAsync(intentId, match: resolution.Match);
  }

  private async Task HandleBlueskyCallbackAsync(Uri url)
  {
    if (await serviceProvider.GetRequiredService<NativeBlueskyLinkCoordinator>()
        .HandleCallbackAsync(url).ConfigureAwait(true))
    {
      await OpenIntentAsync(NavigationCatalog.SettingsIntentId).ConfigureAwait(true);
    }
  }

  private async Task HandleOAuthCallbackAsync(Uri url)
  {
    var coordinator = serviceProvider.GetRequiredService<NativeOAuthAuthorizationCoordinator>();
    var outcome = await coordinator.HandleCallbackAsync(url).ConfigureAwait(true);
    if (!outcome.Handled) return;
    if (outcome.Purpose == OAuthAuthorizationPurpose.Connect)
    {
      await OpenIntentAsync(NavigationCatalog.SettingsIntentId).ConfigureAwait(true);
    }
    else
    {
      await OpenSessionAsync().ConfigureAwait(true);
    }
  }

  private bool ShouldQueueUntilFeatureFlagsHydrate(NativeDeepLinkResolution resolution) =>
      resolution.Status == NativeDeepLinkStatus.Included &&
      resolution.IntentId == "fediverse" &&
      !resolution.IsVisible &&
      viewerProvider.CurrentViewer.FeatureFlags is null;
}
