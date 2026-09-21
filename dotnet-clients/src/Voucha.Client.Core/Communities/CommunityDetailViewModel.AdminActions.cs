using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private long moderationResultsRequestRevision;

  public Task LoadSavedRepliesAsync(CancellationToken cancellationToken = default) =>
      LoadSavedRepliesCoreAsync(cancellationToken);

  public Task LoadAutomodRecentActionsAsync(
      string? after = null,
      int? limit = 25,
      string? window = null,
      string? source = null,
      CancellationToken cancellationToken = default) =>
      LoadAutomodRecentActionsCoreAsync(after, limit, window, source, cancellationToken);

  public async Task<CommunityModerationResultsResponse?> LoadModerationResultsAsync(
      string postId,
      CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(postId))
    {
      return null;
    }

    var requestRevision = Interlocked.Increment(ref moderationResultsRequestRevision);
    var requestContextRevision = Volatile.Read(ref communityContextRevision);
    var response = await service.FetchModerationResultsAsync(communityIdOrSlug, postId, cancellationToken).ConfigureAwait(true);
    if (cancellationToken.IsCancellationRequested ||
        requestRevision != Volatile.Read(ref moderationResultsRequestRevision) ||
        requestContextRevision != Volatile.Read(ref communityContextRevision))
    {
      return response;
    }
    Moderation = [
      Summary(
          "community-agent-moderations",
          UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesCommunityAgentModerations),
          UiText.Verbatim(localization.FormatNumber(response.CommunityAgentModerations.Count))),
      Summary(
          "platform-moderation",
          UiText.Localized(UiMessageKey.NativeModerationSummaryTitle),
          PlatformModerationStatusText(response.PlatformModeration)),
    ];
    OnPropertyChanged(nameof(Moderation));
    return response;
  }

  private static UiText PlatformModerationStatusText(CommunityPlatformModeration moderation) =>
      moderation.Status switch
      {
        AdminReviewQueueClearanceStatus.Approved => UiText.Localized(UiMessageKey.NativeDotnetModerationApproved),
        AdminReviewQueueClearanceStatus.InReview => UiText.Localized(UiMessageKey.NativeDotnetModerationInReview),
        AdminReviewQueueClearanceStatus.Pending => UiText.Localized(UiMessageKey.NativeDotnetModerationPending),
        AdminReviewQueueClearanceStatus.Rejected => UiText.Localized(UiMessageKey.NativeDotnetModerationRejected),
        _ => UiText.Localized(UiMessageKey.NativeModerationSummaryDispositionIncomplete),
      };

  public Task<bool> UpdatePostTypeSettingsAsync(
      bool? allowReviewPosts = null,
      bool? allowDataPointPosts = null,
      CancellationToken cancellationToken = default) =>
      MutateAndApplyAsync(
          () => service.UpdatePostTypeSettingsAsync(
              communityIdOrSlug,
              new UpdateCommunityPostTypeSettingsRequest(allowReviewPosts, allowDataPointPosts),
              cancellationToken),
          cancellationToken);

  public Task<bool> IssueWarningAsync(
      string userId,
      string reason,
      string? publicMessage = null,
      string? reportId = null,
      bool? resolveReport = null,
      CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.IssueWarningAsync(
              communityIdOrSlug,
              new IssueCommunityWarningRequest(userId, reason, publicMessage, reportId, resolveReport),
              cancellationToken),
          cancellationToken);

  public Task<bool> ResolveModerationReportAsync(
      string reportId,
      string status,
      CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.ResolveModerationReportAsync(communityIdOrSlug, reportId, status, cancellationToken),
          cancellationToken,
          _ =>
          {
            TombstoneResolvedReport(reportId);
            return Task.CompletedTask;
          });

  public Task<bool> ConfirmBanEvasionAsync(string userId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.ConfirmBanEvasionAsync(communityIdOrSlug, userId, cancellationToken), cancellationToken);

  public Task<bool> DismissBanEvasionAsync(string userId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.DismissBanEvasionAsync(communityIdOrSlug, userId, cancellationToken), cancellationToken);
}
