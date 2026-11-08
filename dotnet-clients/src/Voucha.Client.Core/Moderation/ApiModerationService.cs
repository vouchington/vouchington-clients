using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed class ApiModerationService(VouchaApiClient client)
    : IModerationService, IModerationAppealsService, IModerationDisputesService,
      IModerationExposureService
{
  public Task<StaffClusteredModerationReportsResponse> FetchClusteredReportsAsync(string? status = "pending", string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchClusteredModerationReportsAsync(status, after, limit, cancellationToken);

  public Task<StaffFlatModerationReportsResponse> FetchStaffReportsAsync(string? status = "pending", string? sort = null, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchStaffModerationReportsAsync(status, sort, after, limit, cancellationToken);

  public Task<MemberFlatModerationReportsResponse> FetchMemberReportsAsync(string? status = "pending", string? sort = null, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchMemberModerationReportsAsync(status, sort, after, limit, cancellationToken);

  public Task<ModerationReportResolutionResponse> ResolveReportAsync(string reportId, ModerationReportResolution status, CancellationToken cancellationToken = default) =>
      client.ResolveModerationReportAsync(reportId, status, cancellationToken);

  public Task<ModerationReportJudgementResponse> RerunReportJudgementAsync(string reportId, CancellationToken cancellationToken = default) =>
      client.RerunModerationReportJudgementAsync(reportId, cancellationToken);

  public Task<ModerationReportJudgementRunResponse> GetReportJudgementRunAsync(string reportId, string runId, CancellationToken cancellationToken = default) =>
      client.GetModerationReportJudgementRunAsync(reportId, runId, cancellationToken);

  public Task<AdminUserWarningResponse> IssueWarningAsync(IssueAdminWarningRequest request, CancellationToken cancellationToken = default) =>
      client.IssueAdminWarningAsync(request, cancellationToken);

  public Task ConfirmBanEvasionAsync(string communityIdOrSlug, string userId, CancellationToken cancellationToken = default) =>
      client.ConfirmReportBanEvasionAsync(communityIdOrSlug, userId, cancellationToken);

  public Task DismissBanEvasionAsync(string communityIdOrSlug, string userId, CancellationToken cancellationToken = default) =>
      client.DismissReportBanEvasionAsync(communityIdOrSlug, userId, cancellationToken);

  public Task DeleteReportTargetAsync(string entityId, CancellationToken cancellationToken = default) =>
      client.DeletePostAsync(entityId, cancellationToken);

  public Task<ModerationAppealListResponse> FetchMemberAppealsAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchMemberAppealsAsync(status: ModerationAppealStatus.Pending, limit: limit, after: after, cancellationToken: cancellationToken);

  public Task<ModerationDisputeListResponse> FetchMemberDisputesAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchMemberDisputesAsync(status: "pending", limit: limit, after: after, cancellationToken: cancellationToken);

  public Task<StaffModerationAppealListResponse> FetchStaffAppealsAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchStaffAppealsAsync(ModerationAppealStatus.Pending, after, limit, cancellationToken);

  public Task<StaffModerationDisputeListResponse> FetchStaffDisputesAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchStaffDisputesAsync(ModerationDisputeStatus.Pending, after, limit, cancellationToken);

  public Task<AdminReviewQueueResponse> FetchReviewQueueAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchAdminReviewQueueAsync(after, limit, cancellationToken);

  public Task<ClearanceUpdateResponse> UpdatePostClearanceAsync(string postId, PostClearanceAction status, CancellationToken cancellationToken = default) =>
      client.UpdatePostClearanceAsync(postId, status, cancellationToken);

  public Task<ModerationExposureResponse> FetchExposureAsync(
      CancellationToken cancellationToken = default) =>
      client.FetchModerationExposureAsync(cancellationToken);

  public Task<ModerationExposureResponse> RecordReviewQueueRevealAsync(
      string postId,
      CancellationToken cancellationToken = default) =>
      client.RecordModerationRevealAsync(
          postId,
          reportId: null,
          ModerationRevealSurface.ReviewQueue,
          cancellationToken);

  public Task<ModlogResponse> FetchModlogAsync(string? after = null, string? actionType = null, string? communityId = null, string? actorId = null, CancellationToken cancellationToken = default) =>
      client.FetchAdminModlogAsync(new AdminModlogRequest(communityId, actorId, actionType, after), cancellationToken);

  public Task<CommunityModerationAnalyticsResponse> FetchAnalyticsAsync(string range = "30d", CancellationToken cancellationToken = default) =>
      client.FetchAdminModerationAnalyticsAsync(range, cancellationToken);

  public Task<ModerationTransparencyResponse> FetchTransparencyAsync(string range = "30d", string? after = null, CancellationToken cancellationToken = default) =>
      client.FetchModerationTransparencyAsync(range, after, cancellationToken);

  public Task<ModerationCaseCountResponse> FetchPersonalCasesAsync(
      string apiPath,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchModerationCaseCountAsync(apiPath, limit, cancellationToken);

  public Task<PersonalCommunityBansResponse> FetchPersonalCommunityBansAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchPersonalCommunityBansAsync(after, limit, cancellationToken);

  public Task<PersonalRemovedPostsResponse> FetchPersonalRemovedPostsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchPersonalRemovedPostsAsync(after, limit, cancellationToken);

  Task<StaffModerationAppealListResponse> IModerationAppealsService.FetchAppealsAsync(ModerationAppealStatus status, string? after, int limit, CancellationToken cancellationToken) =>
      client.FetchStaffAppealsAsync(status, after, limit, cancellationToken);

  public Task<ModerationAppealResponse> FetchAppealAsync(string id, CancellationToken cancellationToken = default) =>
      client.FetchAppealAsync(id, cancellationToken);

  public Task<ModerationAppealResponse> UpdatePublicResponseAsync(string id, string publicResponse, CancellationToken cancellationToken = default) =>
      client.UpdateAppealPublicResponseAsync(id, publicResponse, cancellationToken);

  public Task<ModerationAppealResponse> ApproveAsync(string id, CancellationToken cancellationToken = default) =>
      client.ApproveAppealAsync(id, cancellationToken);

  public Task<ModerationAppealResponse> DeliverAsync(string id, CancellationToken cancellationToken = default) =>
      client.DeliverAppealAsync(id, cancellationToken);

  public Task<ModerationAppealResponse> ResolveAsync(string id, ModerationAppealAction action, CancellationToken cancellationToken = default) =>
      client.ResolveAppealAsync(id, action, cancellationToken);

  public Task<ModerationAppealQueueResponse> RerunResolutionDraftAsync(string id, CancellationToken cancellationToken = default) =>
      client.RerunAppealResolutionDraftAsync(id, cancellationToken);

  Task<StaffModerationDisputeListResponse> IModerationDisputesService.FetchDisputesAsync(
      ModerationDisputeStatus status,
      string? after,
      int limit,
      CancellationToken cancellationToken) =>
      client.FetchStaffDisputesAsync(status, after, limit, cancellationToken);

  public Task<ModerationDisputeResponse> FetchDisputeAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      client.FetchDisputeAsync(id, cancellationToken);

  Task<ModerationDisputeResponse> IModerationDisputesService.UpdatePublicResponseAsync(
      string id,
      string publicResponse,
      CancellationToken cancellationToken) =>
      client.UpdateDisputeAsync(id, publicResponse, internalNotes: null, cancellationToken);

  Task<ModerationDisputeResponse> IModerationDisputesService.ApproveAsync(
      string id,
      CancellationToken cancellationToken) =>
      client.ApproveDisputeAsync(id, cancellationToken);

  Task<ModerationDisputeResponse> IModerationDisputesService.DeliverAsync(
      string id,
      CancellationToken cancellationToken) =>
      client.DeliverDisputeAsync(id, cancellationToken);

  public Task<ModerationDisputeResponse> ResolveAsync(
      string id,
      ModerationDisputeResolutionAction action,
      string? annotation,
      CancellationToken cancellationToken = default) =>
      client.ResolveDisputeAsync(id, action, annotation, cancellationToken);

  Task<ModerationDisputeQueueResponse> IModerationDisputesService.RerunResolutionDraftAsync(
      string id,
      CancellationToken cancellationToken) =>
      client.RerunDisputeResolutionDraftAsync(id, cancellationToken);
}
