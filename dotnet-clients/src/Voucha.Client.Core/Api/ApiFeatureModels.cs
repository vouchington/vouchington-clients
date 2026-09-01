using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056

public sealed record FetchPostsRequest(
    string? Query = null,
    string? PostTypes = null,
    string? Topics = null,
    string? ReviewTopic = null,
    string? Creator = null,
    string? DataPointVertical = null,
    string? Sort = "hot",
    string? After = null,
    int Limit = 25,
    string? SemanticSearchQuery = null);

public sealed record PostResponse(
    [property: JsonPropertyName("post")] Post Post,
    [property: JsonPropertyName("html")] string? Html = null,
    [property: JsonPropertyName("post_metrics")] PostMetrics? PostMetrics = null,
    [property: JsonPropertyName("user")] User? User = null,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, User>? Users = null,
    [property: JsonPropertyName("communities")] IReadOnlyDictionary<string, Community>? Communities = null,
    [property: JsonPropertyName("post_election")] PostElection? PostElection = null,
    [property: JsonPropertyName("election_vote")] ElectionVote? ElectionVote = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>? Bookmarks = null,
    [property: JsonPropertyName("author_aside")] PostDetailAuthorAside? AuthorAside = null,
    [property: JsonPropertyName("link_embed")] UrlEmbed? LinkEmbed = null);

public sealed record PostMutationResponse(
    [property: JsonPropertyName("post")] Post Post,
    [property: JsonPropertyName("community_post_review")] CommunityPostReview? CommunityPostReview = null);

public sealed record CommunityPostReview(
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt,
    [property: JsonPropertyName("rejected_at")] DateTimeOffset? RejectedAt,
    [property: JsonPropertyName("unpublished_at")] DateTimeOffset? UnpublishedAt);

public sealed record TopicResponse(
    [property: JsonPropertyName("topic")] Topic Topic,
    [property: JsonPropertyName("topic_metrics")] TopicMetrics? TopicMetrics = null,
    [property: JsonPropertyName("topic_election")] TopicElection? TopicElection = null,
    [property: JsonPropertyName("election_vote")] ElectionVote? ElectionVote = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null);

public sealed record TopicMetrics(
    [property: JsonPropertyName("count")] IReadOnlyDictionary<string, int>? Count = null,
    [property: JsonPropertyName("viewer_count")] IReadOnlyDictionary<string, int>? ViewerCount = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("ratings")] TopicRatingMetrics? Ratings = null,
    [property: JsonPropertyName("ratings__updated_at")] DateTimeOffset? RatingsUpdatedAt = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, int>? Bookmarks = null,
    [property: JsonPropertyName("bookmarks__updated_at")] DateTimeOffset? BookmarksUpdatedAt = null);

public sealed record TopicRatingMetrics(
    [property: JsonPropertyName("count")] IReadOnlyDictionary<string, int> Count);

public sealed record ReferralProgramValidationInfoResponse(
    [property: JsonPropertyName("validation_info")] ReferralProgramValidationInfo ValidationInfo);

public sealed record ReferralProgramValidationInfo(
    [property: JsonPropertyName("user_help_text")] string? UserHelpText,
    [property: JsonPropertyName("example_urls")] IReadOnlyList<string> ExampleUrls);

public sealed record FollowerDistributionBody
{
  public const int MaximumRecipients = 100;

  private FollowerDistributionBody(string audience, IReadOnlyList<string>? recipientUserIds)
  {
    Audience = audience;
    RecipientUserIds = recipientUserIds;
  }

  [JsonPropertyName("audience")]
  public string Audience { get; }

  [JsonPropertyName("recipient_user_ids")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public IReadOnlyList<string>? RecipientUserIds { get; }

  public static FollowerDistributionBody AllFollowers() => new("all_followers", null);

  public static FollowerDistributionBody Selected(IReadOnlyList<string> recipientUserIds)
  {
    ArgumentNullException.ThrowIfNull(recipientUserIds);
    if (recipientUserIds.Count is < 1 or > MaximumRecipients)
      throw new ArgumentOutOfRangeException(nameof(recipientUserIds));
    if (recipientUserIds.Any(id => !Guid.TryParseExact(id, "D", out _)) || recipientUserIds.Distinct(StringComparer.Ordinal).Count() != recipientUserIds.Count)
      throw new ArgumentException("Recipients must be unique UUID identifiers.", nameof(recipientUserIds));
    return new("selected_followers", recipientUserIds.ToArray());
  }
}

public sealed record FollowerDistributionAcceptedResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("distribution_id")] string DistributionId);

public sealed record ReportBody(
    [property: JsonPropertyName("entityType")] string EntityType,
    [property: JsonPropertyName("entityId")] string EntityId,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("note")] string? Note = null,
    [property: JsonPropertyName("cf_turnstile_response")] string? TurnstileToken = null);

#pragma warning restore CA1054, CA1056
