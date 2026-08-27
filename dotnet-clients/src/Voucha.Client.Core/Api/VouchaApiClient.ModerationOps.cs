using Voucha.Client.Core.Moderation;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<ModerationAppealListResponse> FetchAppealsAsync(
      ModerationAppealStatus? status = null,
      int limit = 25,
      string? after = null,
      bool mine = false,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealListResponse>(
          VouchaApiEndpoints.Appeals(status, limit, after, mine),
          cancellationToken);

  public Task<ModerationAppealResponse> FetchAppealAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealResponse>(VouchaApiEndpoints.Appeal(id), cancellationToken);

  public Task<ModerationAppealResponse> UpdateAppealPublicResponseAsync(
      string id,
      string publicResponse,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealResponse>(
          VouchaApiEndpoints.UpdateAppeal(id, publicResponse: publicResponse),
          cancellationToken);

  public Task<ModerationAppealResponse> ApproveAppealAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealResponse>(VouchaApiEndpoints.AppealApproval(id), cancellationToken);

  public Task<ModerationAppealResponse> DeliverAppealAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealResponse>(VouchaApiEndpoints.AppealDelivery(id), cancellationToken);

  public Task<ModerationAppealResponse> ResolveAppealAsync(
      string id,
      ModerationAppealAction action,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealResponse>(VouchaApiEndpoints.AppealResolution(id, action), cancellationToken);

  public Task<ModerationAppealQueueResponse> RerunAppealResolutionDraftAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealQueueResponse>(VouchaApiEndpoints.AppealResolutionDrafts(id), cancellationToken);

  public Task<ModerationDisputeListResponse> FetchDisputesAsync(
      string? status = null,
      int limit = 25,
      string? after = null,
      bool mine = false,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationDisputeListResponse>(
          VouchaApiEndpoints.Disputes(status, limit, after, mine),
          cancellationToken);

  public Task<StaffClusteredModerationReportsResponse> FetchClusteredModerationReportsAsync(
      string? status = "pending",
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<StaffClusteredModerationReportsResponse>(
          VouchaApiEndpoints.ClusteredModerationReports(status, after, limit),
          cancellationToken);

  public Task<StaffFlatModerationReportsResponse> FetchStaffModerationReportsAsync(
      string? status = "pending",
      string? sort = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<StaffFlatModerationReportsResponse>(
          VouchaApiEndpoints.ModerationReports(status, sort, after, limit),
          cancellationToken);

  public Task<MemberFlatModerationReportsResponse> FetchMemberModerationReportsAsync(
      string? status = "pending",
      string? sort = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<MemberFlatModerationReportsResponse>(
          VouchaApiEndpoints.ModerationReports(status, sort, after, limit),
          cancellationToken);

  public Task<ModerationReportResolutionResponse> ResolveModerationReportAsync(
      string reportId,
      ModerationReportResolution status,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationReportResolutionResponse>(
          VouchaApiEndpoints.ResolveModerationReport(reportId, status), cancellationToken);

  public Task<ModerationReportJudgementResponse> RerunModerationReportJudgementAsync(
      string reportId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationReportJudgementResponse>(
          VouchaApiEndpoints.RerunModerationReportJudgement(reportId), cancellationToken);

  public Task<AdminUserWarningResponse> IssueAdminWarningAsync(
      IssueAdminWarningRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<AdminUserWarningResponse>(VouchaApiEndpoints.IssueAdminWarning(request), cancellationToken);

  public Task ConfirmReportBanEvasionAsync(
      string communityIdOrSlug,
      string userId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ConfirmReportBanEvasion(communityIdOrSlug, userId), cancellationToken);

  public Task DismissReportBanEvasionAsync(
      string communityIdOrSlug,
      string userId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DismissReportBanEvasion(communityIdOrSlug, userId), cancellationToken);

  public Task<AdminReviewQueueResponse> FetchAdminReviewQueueAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<AdminReviewQueueResponse>(
          VouchaApiEndpoints.AdminReviewQueue(after, limit),
          cancellationToken);

  public Task<ClearanceUpdateResponse> UpdatePostClearanceAsync(
      string postId,
      PostClearanceAction status,
      CancellationToken cancellationToken = default) =>
      SendAsync<ClearanceUpdateResponse>(VouchaApiEndpoints.UpdatePostClearance(postId, status), cancellationToken);

  public Task<ModlogResponse> FetchAdminModlogAsync(AdminModlogRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    return SendAsync<ModlogResponse>(
          VouchaApiEndpoints.AdminModlog(request.CommunityId, request.ActorId, request.ActionType, request.After),
          cancellationToken);
  }

  public Task<CommunityModerationAnalyticsResponse> FetchAdminModerationAnalyticsAsync(string range = "30d", CancellationToken cancellationToken = default) =>
      SendAsync<CommunityModerationAnalyticsResponse>(
          VouchaApiEndpoints.AdminModerationAnalytics(range),
          cancellationToken);

  public Task<ModerationTransparencyResponse> FetchModerationTransparencyAsync(string range = "30d", string? after = null, CancellationToken cancellationToken = default) =>
      SendAsync<ModerationTransparencyResponse>(VouchaApiEndpoints.ModerationTransparency(range, after), cancellationToken);

  public Task<ModerationCaseCountResponse> FetchModerationCaseCountAsync(
      string apiPath,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationCaseCountResponse>(
          VouchaApiEndpoints.ModerationCaseCount(apiPath, limit),
          cancellationToken);

  public Task<PersonalCommunityBansResponse> FetchPersonalCommunityBansAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<PersonalCommunityBansResponse>(
          VouchaApiEndpoints.ModerationCaseCount("/api/v1/my/bans", limit, after),
          cancellationToken);

  public Task<PersonalRemovedPostsResponse> FetchPersonalRemovedPostsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<PersonalRemovedPostsResponse>(
          VouchaApiEndpoints.PersonalRemovedPosts(limit, after),
          cancellationToken);
}
