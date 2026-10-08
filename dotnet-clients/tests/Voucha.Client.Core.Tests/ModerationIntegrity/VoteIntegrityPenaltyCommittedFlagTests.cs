using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class VoteIntegrityPenaltyCommittedFlagTests
{
  [Theory]
  [InlineData(IntegrityFlagStatus.Pending)]
  [InlineData(IntegrityFlagStatus.Resolved)]
  [InlineData(IntegrityFlagStatus.All)]
  public async Task PenaltyAppliesReturnedFlagToSelectedStatus(IntegrityFlagStatus status)
  {
    var pending = ModerationIntegrityTestService.VoteFlag("flag-1");
    var resolved = pending with { Resolution = "penalized", ResolvedAt = DateTimeOffset.UtcNow };
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.VotePage([pending])),
      PenalizeVotes = (_, _) => Task.FromResult(new VoteIntegrityPenaltyApplicationResponse(resolved, 3)),
      FetchVotePenalties = (_, _, flagId, _) => Task.FromResult(new VoteIntegrityPenaltiesResponse(
          [], new PageInfo(null, false, null), new VoteIntegrityPenaltyFilterScope("flag", flagId))),
    };
    var viewModel = new VoteIntegrityViewModel(service, new NavigationViewer(true, ["administrator"]));
    if (status == IntegrityFlagStatus.Pending) await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    else await viewModel.SelectStatusAsync(status, TestContext.Current.CancellationToken);

    await viewModel.ApplyVoteRingPenaltyAsync(pending.Id, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.PenalizedUserCount(pending.Id));
    if (status == IntegrityFlagStatus.Pending) Assert.Empty(viewModel.Items);
    else Assert.Equal("penalized", viewModel.Items.Single().Flag.Resolution);
    Assert.False(viewModel.CanApplyVoteRingPenalty(pending.Id));
  }
}
