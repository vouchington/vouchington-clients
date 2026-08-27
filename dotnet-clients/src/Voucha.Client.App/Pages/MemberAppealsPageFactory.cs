using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class MemberAppealsPageFactory(
    IMemberAppealsService service,
    INavigationViewerProvider viewerProvider,
    MemberAppealDraftStore drafts,
    ITurnstileTokenProvider turnstileTokenProvider,
    IUiLocaleController localeController)
{
  public MemberAppealsPage Create(MemberAppealsRoute route) =>
      new(
          new MemberAppealsViewModel(service, viewerProvider.CurrentViewer, route, drafts),
          turnstileTokenProvider,
          localeController);

  public MemberAppealsPage ReuseOrCreate(
      MemberAppealsPage? existing,
      MemberAppealsRoute route) =>
      existing is not null && existing.Route == route
          ? existing
          : Create(route);
}
