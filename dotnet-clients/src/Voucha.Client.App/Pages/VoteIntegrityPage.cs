using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ModerationIntegrity;

namespace Voucha.Client.App.Pages;

public sealed partial class VoteIntegrityPage
    : IntegrityFlagQueuePageBase<VoteIntegrityViewModel>
{
  public VoteIntegrityPage(VoteIntegrityViewModel viewModel)
      : base(
          viewModel,
          UiMessageKey.NativeDotnetModerationVoteIntegrity,
          "/vote-integrity/flags",
          "/vote-integrity/penalties",
          "vote-integrity")
  {
    InitializeRender(Render);
  }
}
