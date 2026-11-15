using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class VoteIntegrityPenaltiesPage(
    VoteIntegrityPenaltyViewModel viewModel, VoteIntegrityPenaltyReviewViewModel? reviews = null)
    : IntegrityPenaltiesPageBase<VoteWeightPenalty>(
        viewModel, UiMessageKey.NativeSwiftIntegrityVotePenaltiesTitle,
        "vote", "/vote-integrity/flags", "/vote-integrity/penalties",
        reviews is null ? null : new IntegrityPenaltyReviewSection(reviews, "vote"))
{
  protected override IReadOnlyList<IntegrityPenaltyRow> PresentedRows => viewModel.Rows;
}
