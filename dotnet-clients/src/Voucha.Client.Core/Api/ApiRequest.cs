using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ApiRequest(HttpMethod Method, string Path)
{
  public IReadOnlyDictionary<string, string> Query { get; init; } =
      new Dictionary<string, string>(StringComparer.Ordinal);

  public IReadOnlyDictionary<string, string> Headers { get; init; } =
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

  public object? Body { get; init; }

  [JsonIgnore]
  public Func<string, ApiRequest>? ConfirmationFactory { get; init; }

  public ApiRequest ConfirmWith(string? proof, Func<string, ApiRequest> factory, bool required = true) =>
      required && string.IsNullOrWhiteSpace(proof) ? this with { ConfirmationFactory = factory } : this;
}

public sealed record SearchCommunitiesRequest(string Query);

public sealed record ShowCommunityRequest(string Slug);

public sealed record UpsertCommunityAiAgentRequest(string CommunitySlug, string AgentSlug, object? Body = null);

public sealed record SearchTopicsRequest(string Query, string? TopicTypes = null, int? Limit = null);

public sealed record CreateTopicRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("topic_type")] string TopicType,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("hostname")] string? Hostname = null,
    [property: JsonPropertyName("source_topic_alias_id")] string? SourceTopicAliasId = null);

public sealed record FetchRssFeedItemsRequest(
    string FeedType = "any",
    string? MediaType = null,
    string? After = null,
    int Limit = 20,
    bool? HasHackerNewsReferences = null);

public sealed record FetchReferralLinksFeedRequest(
    string FeedType = "follow_users",
    string? After = null,
    int Limit = 25);

public sealed record FetchReferralLinksRequest(string? After = null, int Limit = 25);

public sealed record FetchReferralClickLogsRequest(string? After = null, int Limit = 25);

public sealed record FetchReferralProgramsByLinkCountRequest(int Limit = 25, string? After = null);

public sealed record FetchPrioritizedReferralLinksRequest(string ReferralProgramId, bool All = false);

public sealed record FetchPostsFeedRequest(
    string Feed = "any",
    string? After = null,
    int Limit = 20,
    string? PostTypes = null,
    string? TimeRange = null);

public sealed record EntityRelationsRequest(
    string EntityType,
    string EntityId,
    string Predicate,
    string ObjectType,
    int? MinNetVoteScore = null,
    bool? PositiveNetVoteScore = null,
    int Limit = 100,
    string? Sort = "best",
    string? After = null);

public sealed record CreateEntityRelationRequest(
    string EntityType,
    string EntityId,
    string Predicate,
    string ObjectType,
    string ObjectId);

public sealed record FetchUserFollowingRequest(string UserId, string? After = null, int Limit = 100);

public sealed record FetchUserFollowersRequest(string UserId, string? Query = null, string? After = null, int Limit = 100);

public sealed record FetchRssFeedsRequest(
    string? FeedType = null,
    string? After = null,
    int Limit = 25,
    string? Category = null);

public sealed record FetchUserRssFeedsRequest(
    string UserId,
    string ListType = "following",
    string? FeedType = null,
    string? After = null,
    int Limit = 25);

public sealed record FetchListsRequest(string? After = null, int Limit = 25);

public sealed record FetchListItemsRequest(
    string ListId,
    string? MediaType = null,
    bool? Read = null,
    string? After = null,
    int Limit = 25);
