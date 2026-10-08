namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CommunityModerationQueueResponse> FetchCommunityModerationQueueAsync(
      string idOrSlug,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      FetchCommunityModerationQueueAsync(idOrSlug, after, limit, null, cancellationToken);

  public Task<CommunityModerationQueueResponse> FetchCommunityModerationQueueAsync(
      string idOrSlug,
      string? after,
      int? limit,
      string? source,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityModerationQueueResponse>(
          VouchaApiEndpoints.CommunityModerationQueue(idOrSlug, after, limit, source),
          cancellationToken);

  public Task DismissCommunityAutomodFlagAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DismissCommunityAutomodFlag(idOrSlug, postId), cancellationToken);

  public Task<CommunityPendingReportsResponse> FetchCommunityPendingReportsAsync(
      string idOrSlug,
      string? after = null,
      int? limit = null,
      string sort = "created_at_desc",
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityPendingReportsResponse>(
          VouchaApiEndpoints.CommunityPendingReports(idOrSlug, after, limit, sort),
          cancellationToken);

  public Task ClaimCommunityModerationReportAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClaimCommunityModerationReport(idOrSlug, reportId), cancellationToken);

  public Task ReleaseCommunityModerationReportAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ReleaseCommunityModerationReport(idOrSlug, reportId), cancellationToken);

  public Task ClaimCommunityModerationPostAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClaimCommunityModerationPost(idOrSlug, postId), cancellationToken);

  public Task ReleaseCommunityModerationPostAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ReleaseCommunityModerationPost(idOrSlug, postId), cancellationToken);

  public Task OpenCommunityModerationReportThreadAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.OpenCommunityModerationReportThread(idOrSlug, reportId), cancellationToken);

  public Task OpenCommunityModerationPostThreadAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.OpenCommunityModerationPostThread(idOrSlug, postId), cancellationToken);

  public Task EscalateCommunityModerationReportAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.EscalateCommunityModerationReport(idOrSlug, reportId), cancellationToken);

  public Task DeescalateCommunityModerationReportAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeescalateCommunityModerationReport(idOrSlug, reportId), cancellationToken);

  public Task EscalateCommunityModerationPostAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.EscalateCommunityModerationPost(idOrSlug, postId), cancellationToken);

  public Task DeescalateCommunityModerationPostAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeescalateCommunityModerationPost(idOrSlug, postId), cancellationToken);

  public Task<CommunityModerationAnalyticsResponse> FetchCommunityModerationAnalyticsAsync(
      string idOrSlug,
      string range = "30d",
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityModerationAnalyticsResponse>(
          VouchaApiEndpoints.CommunityModerationAnalytics(idOrSlug, range),
          cancellationToken);

  public Task<ModerationTransparencyResponse> FetchCommunityModerationTransparencyAsync(
      string idOrSlug,
      string range = "30d",
      string? after = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationTransparencyResponse>(
          VouchaApiEndpoints.CommunityModerationTransparency(idOrSlug, range, after),
          cancellationToken);
}
