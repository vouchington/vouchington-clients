using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public interface IModerationService
{
  Task<StaffClusteredModerationReportsResponse> FetchClusteredReportsAsync(string? status = "pending", string? after = null, int limit = 25, CancellationToken cancellationToken = default);

  Task<StaffFlatModerationReportsResponse> FetchStaffReportsAsync(string? status = "pending", string? sort = null, string? after = null, int limit = 25, CancellationToken cancellationToken = default);

  Task<MemberFlatModerationReportsResponse> FetchMemberReportsAsync(string? status = "pending", string? sort = null, string? after = null, int limit = 25, CancellationToken cancellationToken = default);

  Task<ModerationReportResolutionResponse> ResolveReportAsync(string reportId, ModerationReportResolution status, CancellationToken cancellationToken = default);

  Task<ModerationReportJudgementResponse> RerunReportJudgementAsync(string reportId, CancellationToken cancellationToken = default);

  Task<AdminUserWarningResponse> IssueWarningAsync(IssueAdminWarningRequest request, CancellationToken cancellationToken = default);

  Task ConfirmBanEvasionAsync(string communityIdOrSlug, string userId, CancellationToken cancellationToken = default);

  Task DismissBanEvasionAsync(string communityIdOrSlug, string userId, CancellationToken cancellationToken = default);

  Task DeleteReportTargetAsync(string entityId, CancellationToken cancellationToken = default);

  Task<ModerationAppealListResponse> FetchAppealsAsync(bool mine = false, string? after = null, int limit = 25, CancellationToken cancellationToken = default);

  Task<ModerationDisputeListResponse> FetchDisputesAsync(bool mine = false, string? after = null, int limit = 25, CancellationToken cancellationToken = default);

  Task<AdminReviewQueueResponse> FetchReviewQueueAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default);

  Task<ClearanceUpdateResponse> UpdatePostClearanceAsync(string postId, PostClearanceAction status, CancellationToken cancellationToken = default);

  Task<ModlogResponse> FetchModlogAsync(string? after = null, string? actionType = null, string? communityId = null, string? actorId = null, CancellationToken cancellationToken = default);

  Task<CommunityModerationAnalyticsResponse> FetchAnalyticsAsync(string range = "30d", CancellationToken cancellationToken = default);

  Task<ModerationTransparencyResponse> FetchTransparencyAsync(string range = "30d", string? after = null, CancellationToken cancellationToken = default);

  Task<ModerationCaseCountResponse> FetchPersonalCasesAsync(
      string apiPath,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<PersonalCommunityBansResponse> FetchPersonalCommunityBansAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new PersonalCommunityBansResponse([], new PageInfo(null, false, null)));

  Task<PersonalRemovedPostsResponse> FetchPersonalRemovedPostsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new PersonalRemovedPostsResponse([], new PageInfo(null, false, null)));
}
