using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed class ReportIntegrityPenaltyReviewViewModel(
    IModerationIntegrityService service,
    IIntegrityPenaltyReviewService reviews,
    NavigationViewer viewer,
    IUiLocalization? localization = null,
    IUiLocaleController? localeController = null,
    IntegrityCapabilities? capabilities = null)
    : IntegrityPenaltyReviewViewModel<ReportAbusePenalty>(
        (capabilities ?? IntegrityCapabilities.FromViewer(viewer)).CanApplyReportAbusePenalty,
        (capabilities ?? IntegrityCapabilities.FromViewer(viewer)).CanRevokePenalties,
        localization, localeController)
{
  public override IReadOnlyList<IntegrityPenaltyRow> Rows =>
      Items.Select(IntegrityPenaltyPresentation.Report).ToArray();

  protected override string Id(ReportAbusePenalty penalty) => penalty.Id;
  protected override bool IsRevoked(ReportAbusePenalty penalty) => penalty.RevokedAt is not null;
  protected override DateTimeOffset? ReviewDueAt(ReportAbusePenalty penalty) => penalty.ReviewDueAt;
  protected override DateTimeOffset? ReviewedAt(ReportAbusePenalty penalty) => penalty.ReviewedAt;

  protected override async Task<IntegrityQueuePage<ReportAbusePenalty>> FetchDueAsync(
      string? after, CancellationToken cancellationToken)
  {
    var response = await reviews.FetchReportPenaltyReviewsDueAsync(after, PageSize, cancellationToken)
        .ConfigureAwait(false);
    return new(response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
  }

  protected override async Task<ReportAbusePenalty> ReviewExactAsync(
      string id, CancellationToken cancellationToken) =>
      (await reviews.ReviewReportPenaltyAsync(id, cancellationToken).ConfigureAwait(false)).Penalty;

  protected override async Task<ReportAbusePenalty> FetchExactAsync(
      string id, CancellationToken cancellationToken) =>
      (await service.FetchReportPenaltyAsync(id, cancellationToken).ConfigureAwait(false)).Penalty;

  protected override async Task<ReportAbusePenalty> RevokeExactAsync(
      string id, CancellationToken cancellationToken) =>
      (await service.RevokeReportPenaltyAsync(id, cancellationToken).ConfigureAwait(false)).Penalty;
}
