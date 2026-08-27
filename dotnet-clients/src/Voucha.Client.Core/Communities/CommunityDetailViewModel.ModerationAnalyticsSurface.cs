using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private async Task<bool> TryLoadModerationAnalyticsSurfaceAsync(CancellationToken cancellationToken)
  {
    var request = BeginTransparencyRequest();
    if (!CanViewRawModerationAnalytics)
    {
      var transparency = await LoadTransparencyRowsAsync(request, cancellationToken).ConfigureAwait(true);
      if (transparency.Rows is not null && IsCurrentTransparencyContext(request)) Moderation = transparency.Rows;
      return true;
    }

    try
    {
      var response = await service.FetchModerationAnalyticsAsync(request.Community, request.Range, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentTransparencyContext(request)) return true;
      Moderation = RawModerationAnalyticsRows(response);
    }
    catch (VouchaApiException exception) when (exception.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
    {
      if (IsCurrentTransparencyContext(request))
      {
        transparencyPaginationError = null;
        ErrorMessage = null;
        State = LoadState.Loaded;
        Moderation = LockedTransparencyRows();
      }
      return true;
    }

    try
    {
      var transparency = await LoadTransparencyRowsAsync(request, cancellationToken).ConfigureAwait(true);
      if (transparency.Rows is null || !IsCurrentTransparencyContext(request)) return true;
      Moderation = transparency.DenialStatus == System.Net.HttpStatusCode.NotFound ||
          transparency.DenialStatus == System.Net.HttpStatusCode.Forbidden && !HasDurableSiteModerationRole
          ? transparency.Rows
          : Moderation.Concat(transparency.Rows).ToArray();
    }
    catch (VouchaApiException)
    {
    }
    catch (HttpRequestException)
    {
    }
    catch (System.Text.Json.JsonException)
    {
    }
    return true;
  }

  private IReadOnlyList<CommunitySummaryRow> RawModerationAnalyticsRows(CommunityModerationAnalyticsResponse response)
  {
    var unavailable = UiText.Localized(UiMessageKey.NativeDotnetGrowthUnavailable);
    var queueVolume = response.QueueVolume;
    var appeals = response.Appeals;
    var automod = response.AutomodPerformance;
    var friction = response.NewUserFriction;
    return [
      Summary("queue-volume", UiText.Localized(UiMessageKey.NativeDotnetModerationQueueVolume), queueVolume is null ? unavailable : UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesPendingReports, ("count", queueVolume.PendingReports)), queueVolume is null ? unavailable : UiText.Verbatim(localization.FormatNumber(queueVolume.TotalReports))),
      Summary("appeals", UiText.Localized(UiMessageKey.NativeDotnetModerationAppeals), appeals?.SuccessRate is { } successRate ? UiText.Verbatim(localization.FormatPercent(successRate)) : unavailable, appeals is null ? unavailable : UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesAcceptedClosed, ("accepted", appeals.Accepted), ("closed", appeals.TotalClosed))),
      Summary("automod", UiText.Localized(UiMessageKey.NativeDotnetModerationAutomod), automod?.FalsePositiveRate is { } falsePositiveRate ? UiText.Verbatim(localization.FormatPercent(falsePositiveRate)) : unavailable, automod is null ? unavailable : UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesAutoRemoves, ("count", automod.AutoRemoves))),
      Summary("friction", UiText.Localized(UiMessageKey.NativeDotnetModerationNewUserFriction), friction?.RejectionRate is { } rejectionRate ? UiText.Verbatim(localization.FormatPercent(rejectionRate)) : unavailable, friction is null ? unavailable : UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesRejectedFirstPosts, ("rejected", friction.RejectedFirstPosts), ("total", friction.FirstPosts))),
    ];
  }
}
