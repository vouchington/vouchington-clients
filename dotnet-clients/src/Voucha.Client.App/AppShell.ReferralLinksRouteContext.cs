using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.ReferralLinks;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private void SetReferralLinksRouteContext(NativeDeepLinkResolution resolution)
  {
    if (resolution.IntentId != "referral-links" || resolution.Match is not { } match) return;

    referralLinksRouteContextStore.Set(ReferralLinksRouteContext.FromMatch(match));
  }
}
