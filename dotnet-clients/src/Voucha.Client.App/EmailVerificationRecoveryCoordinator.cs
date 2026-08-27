using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App;

public sealed class EmailVerificationRecoveryCoordinator(
    IEmailAddressService service,
    IUiLocalization localization,
    IUiLocaleController localeController)
{
  private readonly IEmailAddressService service = service ?? throw new ArgumentNullException(nameof(service));
  private readonly IUiLocalization localization =
      localization ?? throw new ArgumentNullException(nameof(localization));
  private readonly IUiLocaleController localeController =
      localeController ?? throw new ArgumentNullException(nameof(localeController));
  private bool isPresenting;

  public async Task<bool> PresentIfRequestedAsync(
      Page owner,
      EmailVerificationGatedMutation gate,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(gate);
    return await gate.ConsumeRecoveryRequestAsync(
        _ => PresentAsync(owner),
        cancellationToken).ConfigureAwait(true);
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Reliability",
      "CA2000:Dispose objects before losing scope",
      Justification = "EmailVerificationRecoveryPage owns and disposes the view model when MAUI unloads the modal page.")]
  public async Task PresentAsync(Page owner)
  {
    ArgumentNullException.ThrowIfNull(owner);
    if (isPresenting || owner.Navigation.ModalStack.Any(IsRecoveryModal)) return;
    isPresenting = true;
    try
    {
      await owner.Navigation.PushModalAsync(
          new NavigationPage(
              new EmailVerificationRecoveryPage(
                  new EmailAddressManagerViewModel(service, localization, localeController))));
    }
    finally
    {
      isPresenting = false;
    }
  }

  private static bool IsRecoveryModal(Page page) =>
      page is EmailVerificationRecoveryPage or
      NavigationPage { RootPage: EmailVerificationRecoveryPage };
}
