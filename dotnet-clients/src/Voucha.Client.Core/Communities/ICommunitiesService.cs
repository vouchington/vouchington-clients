using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.Core.Communities;

public partial interface ICommunitiesService
{
  Task<CommunitySearchResponse> SearchAsync(string query, int limit = 25, CancellationToken cancellationToken = default);

  Task<CommunityResponse> FetchDetailAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityMutationResponse> CreateAsync(CreateCommunityRequest request, CancellationToken cancellationToken = default);

  Task<CommunityMutationResponse> UpdateAsync(string idOrSlug, UpdateCommunityRequest request, CancellationToken cancellationToken = default);

  Task<CommunityMembersResponse> FetchMembersAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityPostsResponse> FetchPostsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<RssFeedItemsFeedResponse> FetchNewsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityPinnedPostsResponse> FetchPinnedPostsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityListItemCountsResponse> FetchListItemCountsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityApplicationsResponse> FetchApplicationsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityApplicationQuestionsResponse> FetchApplicationQuestionsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityInvitesResponse> FetchInvitesAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityBansResponse> FetchBansAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityRestrictionsResponse> FetchRestrictionsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<ModlogResponse> FetchModlogAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<ModeratorVacationResponse> FetchModeratorVacationAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityModeratorStatsResponse> FetchModeratorStatsAsync(string idOrSlug, int window = 30, CancellationToken cancellationToken = default);

  Task<CommunityModerationQueueResponse> FetchModerationQueueAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityModmailMessageListResponse> FetchModmailMessagesAsync(
      string idOrSlug,
      string threadId,
      CancellationToken cancellationToken = default);

  Task<CommunityModmailThreadListResponse> FetchModmailAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityModmailMessageListResponse> FetchModmailMessagesPageAsync(
      string idOrSlug,
      string threadId,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      FetchModmailMessagesAsync(idOrSlug, threadId, cancellationToken);

  Task<CommunityModmailThreadListResponse> FetchModmailPageAsync(
      string idOrSlug,
      string? after,
      int? limit,
      CancellationToken cancellationToken = default) =>
      FetchModmailAsync(idOrSlug, cancellationToken);

  Task<CommunityModerationAnalyticsResponse> FetchModerationAnalyticsAsync(
      string idOrSlug,
      string range = ModerationTransparencyRange.Default,
      CancellationToken cancellationToken = default);

  Task<ModerationTransparencyResponse> FetchModerationTransparencyAsync(
      string idOrSlug,
      string range = ModerationTransparencyRange.Default,
      string? after = null,
      CancellationToken cancellationToken = default);

  Task<CommunityAiAgentsResponse> FetchAiAgentsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityAgentPromptsResponse> FetchAgentPromptsAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityAgentPromptHistoryResponse> FetchAgentPromptHistoryAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityAutomodSimulation> SimulateAutomodAsync(string idOrSlug, CommunityAutomodSimulationRequest request, CancellationToken cancellationToken = default);

  Task<CommunityAutomodFeedbackResponse> RecordAutomodFeedbackAsync(string idOrSlug, string sourceKey, CommunityAutomodFeedbackRequest request, CancellationToken cancellationToken = default);

  Task<CommunityMutationResponse> ArchiveAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task<CommunityMutationResponse> UnarchiveAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task JoinAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task LeaveAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task UpdateMemberRoleAsync(string idOrSlug, string userId, string role, CancellationToken cancellationToken = default);

  Task RemoveMemberAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default);

  Task TransferOwnershipAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default);

  Task AddListItemAsync(string idOrSlug, CommunityListItemRequest request, CancellationToken cancellationToken = default);

  Task RemoveListItemAsync(string idOrSlug, string itemType, string itemId, CancellationToken cancellationToken = default);

  Task ApplyAsync(string idOrSlug, ApplyToCommunityRequest request, CancellationToken cancellationToken = default);

  Task ReviewApplicationAsync(string idOrSlug, string applicationId, UpdateCommunityPostReviewRequest request, CancellationToken cancellationToken = default);

  Task SendInviteAsync(string idOrSlug, SendCommunityInviteRequest request, CancellationToken cancellationToken = default);

  Task RevokeInviteAsync(string idOrSlug, string inviteId, CancellationToken cancellationToken = default);

  Task RedeemInviteAsync(RedeemCommunityInviteRequest request, CancellationToken cancellationToken = default);

  Task ReviewPostAsync(string idOrSlug, string postId, UpdateCommunityPostReviewRequest request, CancellationToken cancellationToken = default);

  Task UpdatePinnedPostsAsync(string idOrSlug, CommunityPinnedPostsUpdateRequest request, CancellationToken cancellationToken = default);

  Task BanAsync(string idOrSlug, BanCommunityMemberRequest request, CancellationToken cancellationToken = default);

  Task LiftBanAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default);

  Task ActivateRestrictionsAsync(string idOrSlug, ActivateCommunityRestrictionsRequest request, CancellationToken cancellationToken = default);

  Task LiftRestrictionAsync(string idOrSlug, string restrictionId, CancellationToken cancellationToken = default);

  Task SetModeratorVacationAsync(string idOrSlug, DateTimeOffset? endsAt = null, CancellationToken cancellationToken = default);

  Task SetSuppressCommunityDigestsWhileOnVacationAsync(string idOrSlug, bool suppress, CancellationToken cancellationToken = default);

  Task ClearModeratorVacationAsync(string idOrSlug, CancellationToken cancellationToken = default);

  Task ClaimModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default);

  Task ReleaseModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default);

  Task ClaimModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default);

  Task ReleaseModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default);

  Task OpenModerationReportThreadAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default);

  Task OpenModerationPostThreadAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default);

  Task EscalateModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default);

  Task DeescalateModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default);

  Task EscalateModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default);

  Task DeescalateModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default);
}
