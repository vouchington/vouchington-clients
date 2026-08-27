namespace Voucha.Client.Core.Voting;

using Voucha.Client.Core.Api;

internal static class VoteCalculator
{
  public static (double? Net, int? Up, int? Down, ElectionVoteChoice? Current) Apply(
      ElectionVoteChoice? currentChoice,
      double? voteScoreNet,
      int? voteCountUp,
      int? voteCountDown,
      ElectionVoteChoice? choice)
  {
    var previousSign = Sign(currentChoice);
    var nextSign = Sign(choice);
    var nextUp = (voteCountUp ?? 0) - (previousSign > 0 ? 1 : 0) + (nextSign > 0 ? 1 : 0);
    var nextDown = (voteCountDown ?? 0) - (previousSign < 0 ? 1 : 0) + (nextSign < 0 ? 1 : 0);
    // The API only supplies aggregate weighted net, not the viewer's weight.
    // Optimistically reconcile the unweighted raw counts and leave the
    // server-owned weighted aggregate unchanged until the next load.
    return (voteScoreNet, nextUp, nextDown, choice);
  }

  public static int Sign(ElectionVoteChoice? choice) => choice switch
  {
    ElectionVoteChoice.Vouch or ElectionVoteChoice.Like or ElectionVoteChoice.Support or ElectionVoteChoice.Confirm or ElectionVoteChoice.Accurate => 1,
    ElectionVoteChoice.Dislike or ElectionVoteChoice.Disavow or ElectionVoteChoice.Oppose or ElectionVoteChoice.Dispute or ElectionVoteChoice.Inaccurate => -1,
    _ => 0,
  };
}
