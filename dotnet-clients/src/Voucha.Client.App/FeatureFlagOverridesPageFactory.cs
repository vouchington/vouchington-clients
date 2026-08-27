using Voucha.Client.App.Pages;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.FeatureFlags;

namespace Voucha.Client.App;

public static class FeatureFlagOverridesPageFactory
{
  public static bool TryCreate(IServiceProvider serviceProvider, ISessionStore sessionStore, out Page page)
  {
    if (FeatureFlagOverridePolicy.CanManage(sessionStore.Current.Identity?.Roles))
    {
      page = serviceProvider.GetRequiredService<FeatureFlagOverridesPage>();
      return true;
    }
    page = null!;
    return false;
  }
}
