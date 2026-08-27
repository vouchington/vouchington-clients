using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public enum ElectionVoteChoice
{
  Vouch, Like, Neutral, Dislike, Disavow,
  Support, Oppose,
  Confirm, Dispute,
  Accurate, Inaccurate,
}

public enum ElectionVotePolicy { Sentiment, Recommendation, Relation, Moderation }

public static class ElectionVotePolicies
{
  public static void RequirePostChoice(ElectionVoteChoice choice)
  {
    if (choice is ElectionVoteChoice.Support or ElectionVoteChoice.Oppose) return;
    ElectionVotePolicy.Sentiment.Require(choice);
  }

  public static void Require(this ElectionVotePolicy policy, ElectionVoteChoice choice)
  {
    var allowed = policy switch
    {
      ElectionVotePolicy.Sentiment => choice is ElectionVoteChoice.Vouch or ElectionVoteChoice.Like or ElectionVoteChoice.Neutral or ElectionVoteChoice.Dislike or ElectionVoteChoice.Disavow,
      ElectionVotePolicy.Recommendation => choice is ElectionVoteChoice.Support or ElectionVoteChoice.Oppose,
      ElectionVotePolicy.Relation => choice is ElectionVoteChoice.Confirm or ElectionVoteChoice.Dispute,
      ElectionVotePolicy.Moderation => choice is ElectionVoteChoice.Accurate or ElectionVoteChoice.Inaccurate,
      _ => false,
    };
    if (!allowed) throw new ArgumentOutOfRangeException(nameof(choice), choice, $"{choice} is not valid for {policy} voting.");
  }
}

public sealed record FollowContextUsers(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("users")] IReadOnlyList<User> Users);

public sealed record UserTrustContext(
    [property: JsonPropertyName("positive_by_following")] FollowContextUsers PositiveByFollowing,
    [property: JsonPropertyName("negative_by_following")] FollowContextUsers NegativeByFollowing,
    [property: JsonPropertyName("election_vote")] ElectionVote? ElectionVote);

public sealed record ElectionVote(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("choice")] ElectionVoteChoice Choice,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record PostElection(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("votes_score_net")] double VotesScoreNet,
    [property: JsonPropertyName("votes_count_up")] int VotesCountUp,
    [property: JsonPropertyName("votes_count_down")] int VotesCountDown);

public sealed record TopicElection(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("votes_score_net")] double VotesScoreNet,
    [property: JsonPropertyName("votes_count_up")] int VotesCountUp,
    [property: JsonPropertyName("votes_count_down")] int VotesCountDown);

public sealed record RssFeedItemElection(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("votes_score_net")] double VotesScoreNet,
    [property: JsonPropertyName("votes_count_up")] int VotesCountUp,
    [property: JsonPropertyName("votes_count_down")] int VotesCountDown);

#pragma warning restore CA1054, CA1056, CA1720
