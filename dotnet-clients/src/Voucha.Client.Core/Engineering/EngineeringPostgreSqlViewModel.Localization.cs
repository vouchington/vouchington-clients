using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed partial class EngineeringPostgreSqlViewModel
{
  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(ArticleSyncStatusMessage));
    NotifyRangeErrorMessages();
  }

  public void Dispose()
  {
    if (isDisposed) return;
    isDisposed = true;
    localeSubscription?.Dispose();
    if (sessionStore is not null) sessionStore.SessionChanged -= OnRangeSessionChanged;
    rangeCancellation?.Cancel();
    rangeCancellation?.Dispose();
    rangeCancellation = null;
  }

  private string? FormatArticleSyncStatus()
  {
    if (ArticleSyncStatus is null) return null;
    return ArticleSyncStatus.Status switch
    {
      ActiveStatus => localization.Format(UiMessageKey.NativeDotnetDynamicArticleSyncActive, ("id", ArticleSyncJobId)),
      CompletedStatus when ArticleSyncStatus.Result is { } result => localization.Format(
          UiMessageKey.NativeDotnetDynamicArticleSyncCompleted,
          ("id", ArticleSyncJobId),
          ("created", result.Summary.Created),
          ("updated", result.Summary.Updated),
          ("skipped", result.Summary.Skipped),
          ("errored", result.Summary.Errored)),
      FailedStatus => localization.Format(UiMessageKey.NativeDotnetDynamicArticleSyncFailed, ("id", ArticleSyncJobId), ("error", ArticleSyncStatus.Error)),
      _ => localization.Format(UiMessageKey.NativeDotnetDynamicArticleSyncStatus, ("id", ArticleSyncJobId), ("status", ArticleSyncStatus.Status)),
    };
  }
}
