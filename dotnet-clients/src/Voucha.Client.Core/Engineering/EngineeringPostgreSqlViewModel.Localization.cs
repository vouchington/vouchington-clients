using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed partial class EngineeringPostgreSqlViewModel
{
  public void OnUiLocaleChanged() => OnPropertyChanged(nameof(ArticleSyncStatusMessage));

  public void Dispose() => localeSubscription?.Dispose();

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
