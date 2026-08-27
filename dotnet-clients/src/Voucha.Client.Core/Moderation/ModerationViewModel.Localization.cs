using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationViewModel
{
  public ModerationViewModel(
      IModerationService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose() => localeSubscription?.Dispose();

  private string FormatClearanceStatus(AdminReviewQueueClearanceStatus status) => status switch
  {
    AdminReviewQueueClearanceStatus.Rejected => localization.Localize(UiMessageKey.NativeDotnetModerationRejected),
    AdminReviewQueueClearanceStatus.InReview => localization.Localize(UiMessageKey.NativeDotnetModerationInReview),
    AdminReviewQueueClearanceStatus.Approved => localization.Localize(UiMessageKey.NativeDotnetModerationApproved),
    AdminReviewQueueClearanceStatus.Pending => localization.Localize(UiMessageKey.NativeDotnetModerationPending),
    _ => status.ToString(),
  };

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(Title));
    OnPropertyChanged(nameof(PaginationActionTitle));
    Items = Items.ToArray();
    RelocalizeTransparencyRows();
  }
}
