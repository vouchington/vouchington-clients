using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed class VoteIntegrityPenaltyViewModel(
    IModerationIntegrityService service,
    NavigationViewer viewer,
    IUiLocalization? localization = null,
    IUiLocaleController? localeController = null,
    IntegrityCapabilities? capabilities = null)
    : IntegrityPenaltyLedgerViewModel<VoteWeightPenalty>(
        viewer, localization, localeController, capabilities)
{
  public IReadOnlyList<IntegrityPenaltyRow> Rows =>
      Items.Select(IntegrityPenaltyPresentation.Vote).ToArray();

  protected override string Id(VoteWeightPenalty penalty) => penalty.Id;
  protected override bool IsRevoked(VoteWeightPenalty penalty) => penalty.RevokedAt is not null;

  protected override async Task<IntegrityQueuePage<VoteWeightPenalty>> FetchPageAsync(
      IntegrityPenaltyStatus status, string? after, CancellationToken cancellationToken)
  {
    var response = await service.FetchVotePenaltiesAsync(
        status, after, cancellationToken: cancellationToken).ConfigureAwait(false);
    return new(response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
  }

  protected override async Task<VoteWeightPenalty> FetchExactAsync(
      string penaltyId, CancellationToken cancellationToken) =>
      (await service.FetchVotePenaltyAsync(penaltyId, cancellationToken)
          .ConfigureAwait(false)).Penalty;

  protected override async Task<VoteWeightPenalty> RevokeExactAsync(
      string penaltyId, CancellationToken cancellationToken) =>
      (await service.RevokeVotePenaltyAsync(penaltyId, cancellationToken)
          .ConfigureAwait(false)).Penalty;
}
