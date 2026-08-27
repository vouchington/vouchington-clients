using Voucha.Client.App.Controls;
using Voucha.Client.Core.Profiles;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private async void OnUserTrustVoteClicked(object? sender, EventArgs e)
  {
    if (!viewModel.CanCreateUserTrustVote) return;
    var choice = await SemanticVoteActionSheet.ChooseSentimentAsync(this, viewModel.UserTrustChoice);
    if (choice is null) return;
    await viewModel.VoteUserTrustAsync(choice);
    await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
  }

  private async void OnClearUserTrustVoteClicked(object? sender, EventArgs e)
  {
    if (!viewModel.CanClearUserTrustVote) return;
    await viewModel.VoteUserTrustAsync(null);
    await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
  }

  private async void OnUserTagVoteClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: ProfileUserTagRow row } button) return;
    if (!viewModel.CanCreateUserTagVotes) return;
    button.IsEnabled = false;
    try
    {
      var choice = await SemanticVoteActionSheet.ChooseRelationAsync(this);
      if (choice is not null) await viewModel.VoteUserTagAsync(row.Id, choice);
    }
    finally
    {
      button.IsEnabled = true;
    }
  }

  private async void OnClearUserTagVoteClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: ProfileUserTagRow row }) return;
    if (!viewModel.CanClearUserTagVotes || row.MyVote is null) return;
    await viewModel.VoteUserTagAsync(row.Id, null);
  }
}
