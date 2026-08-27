using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Voting;

internal static class ElectionSidecars
{
  public static TopicElection? TopicElectionFor(
      IReadOnlyDictionary<string, TopicElection>? elections,
      string? topicId)
  {
    if (elections is null || topicId is null) return null;
    elections.TryGetValue(topicId, out var election);
    return election;
  }

  public static ElectionVote? ElectionVoteFor(
      IReadOnlyDictionary<string, ElectionVote>? votes,
      string? topicId)
  {
    if (votes is null || topicId is null) return null;
    votes.TryGetValue(topicId, out var vote);
    return vote;
  }
}
