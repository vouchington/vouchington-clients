using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
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

    var response = await service.FetchModerationResultsAsync(communityIdOrSlug, postId, cancellationToken).ConfigureAwait(true);
    Moderation = [
      Summary(
          "community-agent-moderations",
          UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesCommunityAgentModerations),
          UiText.Verbatim(localization.FormatNumber(response.CommunityAgentModerations.Count))),
      Summary(
          "openai-moderation",
          UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesOpenAiModeration),
          UiText.Localized(response.OpenAIModeration?.Flagged switch
          {
            true => UiMessageKey.NativeDotnetCsharpCommunitiesFlagged,
            false => UiMessageKey.NativeDotnetCsharpCommunitiesClear,
            null => UiMessageKey.NativeDotnetCsharpCommunitiesUnknown,
          })),
    ];
    OnPropertyChanged(nameof(Moderation));
    return response;
  }

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
