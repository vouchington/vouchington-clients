using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public CommunityDetailViewModel(
      ICommunitiesService service,
      ISessionStore? sessionStore = null,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.sessionStore = sessionStore;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }
}
