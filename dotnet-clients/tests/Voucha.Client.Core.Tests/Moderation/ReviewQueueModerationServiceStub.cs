using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.Core.Tests.Moderation;

internal abstract class ReviewQueueModerationServiceStub : IModerationService
{
  public virtual Task<StaffClusteredModerationReportsResponse> FetchClusteredReportsAsync(string? status = "pending", string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<StaffFlatModerationReportsResponse> FetchStaffReportsAsync(string? status = "pending", string? sort = null, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<MemberFlatModerationReportsResponse> FetchMemberReportsAsync(string? status = "pending", string? sort = null, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<ModerationReportResolutionResponse> ResolveReportAsync(string reportId, ModerationReportResolution status, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<ModerationReportJudgementResponse> RerunReportJudgementAsync(string reportId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<AdminUserWarningResponse> IssueWarningAsync(IssueAdminWarningRequest request, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task ConfirmBanEvasionAsync(string communityIdOrSlug, string userId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task DismissBanEvasionAsync(string communityIdOrSlug, string userId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task DeleteReportTargetAsync(string entityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<ModerationAppealListResponse> FetchAppealsAsync(bool mine = false, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<ModerationDisputeListResponse> FetchDisputesAsync(bool mine = false, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<AdminReviewQueueResponse> FetchReviewQueueAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<ClearanceUpdateResponse> UpdatePostClearanceAsync(string postId, PostClearanceAction status, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<ModlogResponse> FetchModlogAsync(string? after = null, string? actionType = null, string? communityId = null, string? actorId = null, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<CommunityModerationAnalyticsResponse> FetchAnalyticsAsync(string range = "30d", CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<ModerationTransparencyResponse> FetchTransparencyAsync(string range = "30d", string? after = null, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public virtual Task<ModerationCaseCountResponse> FetchPersonalCasesAsync(
      string apiPath,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
}
