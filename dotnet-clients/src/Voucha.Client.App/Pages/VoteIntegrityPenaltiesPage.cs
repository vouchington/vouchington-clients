using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class VoteIntegrityPenaltiesPage(VoteIntegrityPenaltyViewModel viewModel)
    : IntegrityPenaltiesPageBase<VoteWeightPenalty>(
        viewModel, UiMessageKey.NativeSwiftIntegrityVotePenaltiesTitle,
        "vote", "/vote-integrity/flags", "/vote-integrity/penalties")
{
  protected override IReadOnlyList<IntegrityPenaltyRow> PresentedRows => viewModel.Rows;
}
