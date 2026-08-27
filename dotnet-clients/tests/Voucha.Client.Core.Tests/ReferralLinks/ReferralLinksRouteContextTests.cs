using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.ReferralLinks;
using Xunit;

namespace Voucha.Client.Core.Tests.ReferralLinks;

public sealed class ReferralLinksRouteContextTests
{
  [Theory]
  [InlineData("/feed/referral-links", "/feed/referral-links", ReferralLinksMode.Following)]
  [InlineData("/feed/referral-links/mutual", "/feed/referral-links/mutual", ReferralLinksMode.Mutual)]
  [InlineData("/my/referral-links", "/my/referral-links", ReferralLinksMode.Mine)]
  [InlineData("/my/referrals", "/my/referrals", ReferralLinksMode.Analytics)]
  [InlineData("/referral-programs", "/referral-programs", ReferralLinksMode.Programs)]
  public void FromMatchMapsReferralRoutesToNativeModes(
      string path,
      string template,
      ReferralLinksMode expectedMode)
  {
    var context = ReferralLinksRouteContext.FromMatch(new NativeRouteMatch(
        path,
        template,
        new Dictionary<string, string>(),
        new Dictionary<string, string>()));

    Assert.Equal(expectedMode, context.Mode);
  }

  [Fact]
  public void StoreConsumesContextOnce()
  {
    var store = new ReferralLinksRouteContextStore();
    var context = new ReferralLinksRouteContext(ReferralLinksMode.Mine);

    store.Set(context);

    Assert.Same(context, store.Consume());
    Assert.Null(store.Consume());
  }
}
