using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed class VoteIntegrityPenaltyReviewViewModel(
    IModerationIntegrityService service,
    IIntegrityPenaltyReviewService reviews,
    NavigationViewer viewer,
    IUiLocalization? localization = null,
    IUiLocaleController? localeController = null,
    IntegrityCapabilities? capabilities = null)
    : IntegrityPenaltyReviewViewModel<VoteWeightPenalty>(
        (capabilities ?? IntegrityCapabilities.FromViewer(viewer)).CanApplyVoteRingPenalty,
        (capabilities ?? IntegrityCapabilities.FromViewer(viewer)).CanRevokePenalties,
        localization, localeController)
{
  public override IReadOnlyList<IntegrityPenaltyRow> Rows =>
      Items.Select(IntegrityPenaltyPresentation.Vote).ToArray();

  protected override string Id(VoteWeightPenalty penalty) => penalty.Id;
  protected override bool IsRevoked(VoteWeightPenalty penalty) => penalty.RevokedAt is not null;
  protected override DateTimeOffset? ReviewDueAt(VoteWeightPenalty penalty) => penalty.ReviewDueAt;
  protected override DateTimeOffset? ReviewedAt(VoteWeightPenalty penalty) => penalty.ReviewedAt;

  protected override async Task<IntegrityQueuePage<VoteWeightPenalty>> FetchDueAsync(
      string? after, CancellationToken cancellationToken)
  {
    var response = await reviews.FetchVotePenaltyReviewsDueAsync(after, PageSize, cancellationToken)
        .ConfigureAwait(false);
    return new(response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
  }

  protected override async Task<VoteWeightPenalty> ReviewExactAsync(
      string id, CancellationToken cancellationToken) =>
      (await reviews.ReviewVotePenaltyAsync(id, cancellationToken).ConfigureAwait(false)).Penalty;

  protected override async Task<VoteWeightPenalty> FetchExactAsync(
      string id, CancellationToken cancellationToken) =>
      (await service.FetchVotePenaltyAsync(id, cancellationToken).ConfigureAwait(false)).Penalty;

  protected override async Task<VoteWeightPenalty> RevokeExactAsync(
      string id, CancellationToken cancellationToken) =>
      (await service.RevokeVotePenaltyAsync(id, cancellationToken).ConfigureAwait(false)).Penalty;
}
