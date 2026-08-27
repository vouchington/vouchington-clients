using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Voting;

namespace Voucha.Client.Core.TopicRecommendations;

public sealed partial class TopicRecommendationDetailViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Recommendation vote failures are restored into rendered detail state.")]
  public async Task VoteAsync(ElectionVoteChoice? choice, CancellationToken cancellationToken = default)
  {
    if (Detail is null || Election is null || IsVoting) return;
    var previous = (Election, CurrentVoteChoice);
    IsVoting = true;
    ErrorMessage = null;
    ApplyVote(choice);
    try
    {
      await EmailVerificationGate.RunAsync(
          () => choice is { } selected
              ? service.VoteAsync(recommendationId, selected, cancellationToken)
              : service.ClearVoteAsync(recommendationId, cancellationToken),
          ex => RestoreVote(previous, ex.Message)).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RestoreVote(previous, null);
    }
    catch (Exception ex)
    {
      RestoreVote(previous, ex.Message);
    }
    finally
    {
      IsVoting = false;
    }
  }

  private void ApplyVote(ElectionVoteChoice? choice)
  {
    var election = Election!;
    var vote = VoteCalculator.Apply(
        CurrentVoteChoice,
        election.VotesScoreNet,
        election.VotesCountUp,
        election.VotesCountDown,
        choice);
    Election = election with
    {
      VotesScoreNet = vote.Net ?? election.VotesScoreNet,
      VotesCountUp = vote.Up ?? election.VotesCountUp,
      VotesCountDown = vote.Down ?? election.VotesCountDown,
    };
    CurrentVoteChoice = vote.Current;
  }

  private void RestoreVote((PostElection? Election, ElectionVoteChoice? Choice) previous, string? error)
  {
    Election = previous.Election;
    CurrentVoteChoice = previous.Choice;
    ErrorMessage = error;
  }
}
