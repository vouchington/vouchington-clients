using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class TopicRecommendationDetailPageSourceTests
{
  [Fact]
  public void VoteActionsBindSessionPermissionsAndInFlightState()
  {
    var source = AppSource.Read("Pages", "TopicRecommendationDetailPage.cs");

    Assert.Contains("sessionStore.Current.CanCastPublicVotes() && viewModel.CanCastVote", source, StringComparison.Ordinal);
    Assert.Contains("sessionStore.Current.CanClearPublicVote(viewModel.CurrentVoteChoice) &&", source, StringComparison.Ordinal);
    Assert.Contains("viewModel.CanClearVote", source, StringComparison.Ordinal);
    Assert.Contains("nameof(TopicRecommendationDetailViewModel.IsVoting)", source, StringComparison.Ordinal);
    Assert.Contains("if (!vote.IsEnabled) return;", source, StringComparison.Ordinal);
    Assert.Contains("if (!clearVote.IsEnabled) return;", source, StringComparison.Ordinal);
  }
}
