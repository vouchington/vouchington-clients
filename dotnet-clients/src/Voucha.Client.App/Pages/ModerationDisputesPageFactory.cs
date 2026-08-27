using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public sealed class ModerationDisputesPageFactory(
    IModerationDisputesService service,
    INavigationViewerProvider viewerProvider,
    IUiLocaleController localeController)
{
  public ModerationDisputesPage Create() =>
      new(new ModerationDisputesViewModel(
          service,
          viewerProvider.CurrentViewer,
          localeController));
}
