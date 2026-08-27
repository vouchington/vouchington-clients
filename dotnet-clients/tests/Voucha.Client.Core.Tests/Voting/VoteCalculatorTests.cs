using Voucha.Client.Core.Api;
using Voucha.Client.Core.Voting;
using Xunit;

namespace Voucha.Client.Core.Tests.Voting;

public sealed class VoteCalculatorTests
{
  [Theory]
  [InlineData(null, ElectionVoteChoice.Vouch)]
  [InlineData(ElectionVoteChoice.Vouch, ElectionVoteChoice.Dislike)]
  [InlineData(ElectionVoteChoice.Disavow, ElectionVoteChoice.Vouch)]
  [InlineData(ElectionVoteChoice.Confirm, ElectionVoteChoice.Dispute)]
  public void ApplyPreservesTheServerOwnedWeightedNetWhileReconcilingRawCounts(
      ElectionVoteChoice? previous,
      ElectionVoteChoice? next)
  {
    var actual = VoteCalculator.Apply(previous, 2, 4, 1, next);

    Assert.Equal(2, actual.Net);
  }

  [Fact]
  public void ApplyKeepsAnUnknownNetUnknownWhileReconcilingCounts()
  {
    var actual = VoteCalculator.Apply(ElectionVoteChoice.Like, null, 4, 1, ElectionVoteChoice.Dislike);

    Assert.Null(actual.Net);
    Assert.Equal(3, actual.Up);
    Assert.Equal(2, actual.Down);
  }
}
