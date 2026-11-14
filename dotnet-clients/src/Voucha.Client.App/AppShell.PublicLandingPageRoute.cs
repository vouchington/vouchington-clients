using Voucha.Client.App.Pages;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenPublicLandingPageRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (!LandingPagesDeepLinkPolicy.TryGetPublicTarget(resolution.Match, out var username, out var slug)) return null;
    var client = serviceProvider.GetRequiredService<VouchaApiClient>();
    var model = new PublicLandingPageViewModel(token => client.FetchPublicLandingPageAsync(username!, slug, token),
        serviceProvider.GetRequiredService<ISessionStore>(), serviceProvider.GetRequiredService<IProductSurfaceAnalytics>());
    return Navigation.PushAsync(new PublicLandingPagePage(model, async target =>
    {
      if (target.ReviewIdOrSlug is { } review) await OpenPostDetailAsync(review, null).ConfigureAwait(true);
      else if (target.ExternalUrl is { } url) await Launcher.OpenAsync(url).ConfigureAwait(true);
    }));
  }
}
