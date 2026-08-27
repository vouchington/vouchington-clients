using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.FeatureFlags;

public sealed class FeatureFlagNavigationBinding
{
  public FeatureFlagNavigationBinding(
      FeatureFlagState state,
      ISessionStore sessionStore,
      MutableNavigationViewerProvider viewerProvider)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(sessionStore);
    ArgumentNullException.ThrowIfNull(viewerProvider);
    state.Changed += (_, args) => viewerProvider.SetViewer(
        NavigationCatalog.FromIdentity(sessionStore.Current.Identity, args.EffectiveFlags));
  }
}
