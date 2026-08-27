using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.Core.Communities;

public sealed partial class ApiCommunitiesService : ICommunitiesService
{
  private readonly VouchaApiClient client;

  public ApiCommunitiesService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<CommunitySearchResponse> SearchAsync(string query, int limit = 25, CancellationToken cancellationToken = default) =>
      client.SendAsync<CommunitySearchResponse>(
          VouchaApiEndpoints.SearchCommunities(query, limit == 25 ? null : limit),
          cancellationToken);

  public Task<CommunityResponse> FetchDetailAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityAsync(new ShowCommunityRequest(idOrSlug), cancellationToken);

  public Task<CommunityMutationResponse> CreateAsync(CreateCommunityRequest request, CancellationToken cancellationToken = default) =>
      client.CreateCommunityAsync(request, cancellationToken);

  public Task<CommunityMutationResponse> UpdateAsync(string idOrSlug, UpdateCommunityRequest request, CancellationToken cancellationToken = default) =>
      client.UpdateCommunityAsync(idOrSlug, request, cancellationToken);

  public Task<CommunityMembersResponse> FetchMembersAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityMembersAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunityPostsResponse> FetchPostsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityPostsAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<RssFeedItemsFeedResponse> FetchNewsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityNewsAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunityPinnedPostsResponse> FetchPinnedPostsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityPinnedPostsAsync(idOrSlug, cancellationToken);

  public Task<CommunityListItemCountsResponse> FetchListItemCountsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityListItemCountsAsync(idOrSlug, cancellationToken);

  public Task<CommunityApplicationsResponse> FetchApplicationsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityApplicationsAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunityApplicationQuestionsResponse> FetchApplicationQuestionsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityApplicationQuestionsAsync(idOrSlug, cancellationToken);

  public Task<CommunityInvitesResponse> FetchInvitesAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityInvitesAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunityBansResponse> FetchBansAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityBansAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunityRestrictionsResponse> FetchRestrictionsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityRestrictionsAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<ModlogResponse> FetchModlogAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityModlogAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<ModeratorVacationResponse> FetchModeratorVacationAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityModeratorVacationAsync(idOrSlug, cancellationToken);

  public Task<CommunityModeratorStatsResponse> FetchModeratorStatsAsync(
      string idOrSlug,
      int window = 30,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityModeratorStatsAsync(idOrSlug, window, cancellationToken);

  public Task<CommunityModerationQueueResponse> FetchModerationQueueAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityModerationQueueAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunityModerationAnalyticsResponse> FetchModerationAnalyticsAsync(
      string idOrSlug,
      string range = ModerationTransparencyRange.Default,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityModerationAnalyticsAsync(idOrSlug, range, cancellationToken);

  public Task<ModerationTransparencyResponse> FetchModerationTransparencyAsync(
      string idOrSlug,
      string range = ModerationTransparencyRange.Default,
      string? after = null,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityModerationTransparencyAsync(idOrSlug, range, after, cancellationToken);

  public Task<CommunityAiAgentsResponse> FetchAiAgentsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityAiAgentsAsync(idOrSlug, cancellationToken);

  public Task<CommunityAgentPromptsResponse> FetchAgentPromptsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.FetchCommunityAgentPromptsAsync(idOrSlug, cancellationToken);

  public Task<CommunityAgentPromptHistoryResponse> FetchAgentPromptHistoryAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchCommunityAgentPromptHistoryAsync(idOrSlug, cancellationToken: cancellationToken);

  public Task<CommunityMutationResponse> ArchiveAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.ArchiveCommunityAsync(idOrSlug, cancellationToken);

  public Task<CommunityMutationResponse> UnarchiveAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.UnarchiveCommunityAsync(idOrSlug, cancellationToken);

  public Task JoinAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.JoinCommunityAsync(idOrSlug, cancellationToken);

  public Task LeaveAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      client.LeaveCommunityAsync(idOrSlug, cancellationToken);

  public Task UpdateMemberRoleAsync(string idOrSlug, string userId, string role, CancellationToken cancellationToken = default) =>
      client.SendAsync(
          VouchaApiEndpoints.UpdateCommunityMemberRole(idOrSlug, userId, new UpdateCommunityMemberRoleRequest(role)),
          cancellationToken);

  public Task RemoveMemberAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.RemoveCommunityMember(idOrSlug, userId), cancellationToken);

  public Task TransferOwnershipAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.TransferCommunityOwnership(idOrSlug, new TransferCommunityOwnershipRequest(userId)), cancellationToken);

  public Task AddListItemAsync(string idOrSlug, CommunityListItemRequest request, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.AddCommunityListItem(idOrSlug, request), cancellationToken);

  public Task RemoveListItemAsync(string idOrSlug, string itemType, string itemId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.RemoveCommunityListItem(idOrSlug, itemType, itemId), cancellationToken);

  public Task ApplyAsync(string idOrSlug, ApplyToCommunityRequest request, CancellationToken cancellationToken = default) =>
      client.ApplyToCommunityAsync(idOrSlug, request, cancellationToken);

  public Task ReviewApplicationAsync(string idOrSlug, string applicationId, UpdateCommunityPostReviewRequest request, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ReviewCommunityApplication(idOrSlug, applicationId, request), cancellationToken);

  public Task SendInviteAsync(string idOrSlug, SendCommunityInviteRequest request, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.SendCommunityInvite(idOrSlug, request), cancellationToken);

  public Task RevokeInviteAsync(string idOrSlug, string inviteId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.RevokeCommunityInvite(idOrSlug, inviteId), cancellationToken);

  public Task RedeemInviteAsync(RedeemCommunityInviteRequest request, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.RedeemCommunityInvite(request), cancellationToken);

  public Task ReviewPostAsync(string idOrSlug, string postId, UpdateCommunityPostReviewRequest request, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ReviewCommunityPost(idOrSlug, postId, request), cancellationToken);

  public Task UpdatePinnedPostsAsync(string idOrSlug, CommunityPinnedPostsUpdateRequest request, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.UpdateCommunityPinnedPosts(idOrSlug, request), cancellationToken);

  public Task BanAsync(string idOrSlug, BanCommunityMemberRequest request, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.BanCommunityMember(idOrSlug, request), cancellationToken);

  public Task LiftBanAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.LiftCommunityBan(idOrSlug, userId), cancellationToken);

  public Task ActivateRestrictionsAsync(string idOrSlug, ActivateCommunityRestrictionsRequest request, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ActivateCommunityRestrictions(idOrSlug, request), cancellationToken);

  public Task LiftRestrictionAsync(string idOrSlug, string restrictionId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.LiftCommunityRestriction(idOrSlug, restrictionId), cancellationToken);

  public Task ClaimModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ClaimCommunityModerationReport(idOrSlug, reportId), cancellationToken);

  public Task ReleaseModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ReleaseCommunityModerationReport(idOrSlug, reportId), cancellationToken);

  public Task ClaimModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ClaimCommunityModerationPost(idOrSlug, postId), cancellationToken);

  public Task ReleaseModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ReleaseCommunityModerationPost(idOrSlug, postId), cancellationToken);

  public Task OpenModerationReportThreadAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.OpenCommunityModerationReportThread(idOrSlug, reportId), cancellationToken);

  public Task OpenModerationPostThreadAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.OpenCommunityModerationPostThread(idOrSlug, postId), cancellationToken);

  public Task EscalateModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.EscalateCommunityModerationReport(idOrSlug, reportId), cancellationToken);

  public Task DeescalateModerationReportAsync(string idOrSlug, string reportId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.DeescalateCommunityModerationReport(idOrSlug, reportId), cancellationToken);

  public Task EscalateModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.EscalateCommunityModerationPost(idOrSlug, postId), cancellationToken);

  public Task DeescalateModerationPostAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.DeescalateCommunityModerationPost(idOrSlug, postId), cancellationToken);
}
