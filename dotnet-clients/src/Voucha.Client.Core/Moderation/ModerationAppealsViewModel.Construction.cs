using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  public ModerationAppealsViewModel(
      IModerationAppealsService service,
      NavigationViewer viewer,
      IUiLocaleController? localeController = null,
      INavigationViewerProvider? viewerProvider = null,
      AppealAIRerunAdmissionStore? admissionStore = null,
      Uri? apiBaseUrl = null)
      : this(
          service,
          viewer,
          20,
          token => Task.Delay(TimeSpan.FromSeconds(3), token),
          localeController,
          viewerProvider,
          admissionStore,
          apiBaseUrl)
  {
  }

  internal ModerationAppealsViewModel(
      IModerationAppealsService service,
      NavigationViewer viewer,
      int rerunPollAttempts,
      Func<CancellationToken, Task> rerunPollDelay,
      IUiLocaleController? localeController = null,
      INavigationViewerProvider? viewerProvider = null,
      AppealAIRerunAdmissionStore? admissionStore = null,
      Uri? apiBaseUrl = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    ArgumentNullException.ThrowIfNull(viewer);
    localization = localeController is null ? UiLocalization.English : new UiLocalization(localeController);
    this.viewer = viewer;
    this.viewerProvider = viewerProvider;
    if (viewerProvider is not null) viewerProvider.ViewerChanged += OnViewerChanged;
    this.admissionStore = admissionStore ?? new AppealAIRerunAdmissionStore();
    admissionOwner = admissionStore is null && apiBaseUrl is null
        ? new AppealAIRerunAdmissionStore.OwnerKey(Guid.NewGuid().ToString(), "", "", 0, "")
        : AppealAIRerunAdmissionStore.ForOwner(viewer.IdentityId, apiBaseUrl);
    IsSignedIn = viewer.IsAuthenticated;
    IsAdministrator = viewer.Roles.Contains("administrator", StringComparer.Ordinal);
    IsModerator = viewer.Roles.Contains("moderator", StringComparer.Ordinal);
    this.rerunPollAttempts = rerunPollAttempts;
    this.rerunPollDelay = rerunPollDelay;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }
}
