namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<ModerationReportDetailResponse> GetModerationReportAsync(
      string reportId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationReportDetailResponse>(
          VouchaApiEndpoints.ModerationReport(reportId), cancellationToken);

  public Task<ModerationAppealResolutionDraftRunResponse> GetAppealResolutionDraftRunAsync(
      string appealId,
      string runId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealResolutionDraftRunResponse>(
          VouchaApiEndpoints.AppealResolutionDraftRun(appealId, runId), cancellationToken);
}
