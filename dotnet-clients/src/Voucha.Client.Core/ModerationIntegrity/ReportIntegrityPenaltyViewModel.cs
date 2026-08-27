using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed class ReportIntegrityPenaltyViewModel(
    IModerationIntegrityService service,
    NavigationViewer viewer,
    IUiLocalization? localization = null,
    IUiLocaleController? localeController = null,
    IntegrityCapabilities? capabilities = null)
    : IntegrityPenaltyLedgerViewModel<ReportAbusePenalty>(
        viewer, localization, localeController, capabilities)
{
  public IReadOnlyList<IntegrityPenaltyRow> Rows =>
      Items.Select(IntegrityPenaltyPresentation.Report).ToArray();

  protected override string Id(ReportAbusePenalty penalty) => penalty.Id;
  protected override bool IsRevoked(ReportAbusePenalty penalty) => penalty.RevokedAt is not null;

  protected override async Task<IntegrityQueuePage<ReportAbusePenalty>> FetchPageAsync(
      IntegrityPenaltyStatus status, string? after, CancellationToken cancellationToken)
  {
    var response = await service.FetchReportPenaltiesAsync(
        status, after, cancellationToken: cancellationToken).ConfigureAwait(false);
    return new(response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
  }

  protected override async Task<ReportAbusePenalty> FetchExactAsync(
      string penaltyId, CancellationToken cancellationToken) =>
      (await service.FetchReportPenaltyAsync(penaltyId, cancellationToken)
          .ConfigureAwait(false)).Penalty;

  protected override async Task<ReportAbusePenalty> RevokeExactAsync(
      string penaltyId, CancellationToken cancellationToken) =>
      (await service.RevokeReportPenaltyAsync(penaltyId, cancellationToken)
          .ConfigureAwait(false)).Penalty;
}
