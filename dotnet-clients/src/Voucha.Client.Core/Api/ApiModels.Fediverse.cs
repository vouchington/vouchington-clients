using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record FediverseSearchResponse(
    [property: JsonPropertyName("buckets")] IReadOnlyList<FediverseSearchBucket> Buckets);

public sealed record FediverseSearchBucket(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("items")] IReadOnlyList<FediverseSearchResult> Items,
    [property: JsonPropertyName("next_cursor")] string? NextCursor = null,
    [property: JsonPropertyName("error_code")] string? ErrorCode = null);

public sealed record FediverseSearchResult(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("result_type")] string ResultType,
    [property: JsonPropertyName("external_url")] Uri ExternalUrl,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("author_name")] string? AuthorName = null,
    [property: JsonPropertyName("author_url")] Uri? AuthorUrl = null,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt = null,
    [property: JsonPropertyName("thumbnail_url")] Uri? ThumbnailUrl = null,
    [property: JsonPropertyName("source_hostname")] string? SourceHostname = null)
{
  public string Id => ExternalUrl.ToString();
}

public sealed record BeginBlueskyAccountLinkResponse(
    [property: JsonPropertyName("redirect_url")] Uri RedirectUrl,
    [property: JsonPropertyName("flow_id")] string? FlowId = null);

public sealed record FediverseInstanceAttributes(
    [property: JsonPropertyName("software")] string? Software,
    [property: JsonPropertyName("nodeinfo_software_version")] string? SoftwareVersion,
    [property: JsonPropertyName("protocol")] string? Protocol,
    [property: JsonPropertyName("total_users")] int? TotalUsers,
    [property: JsonPropertyName("monthly_active_users")] int? MonthlyActiveUsers,
    [property: JsonPropertyName("is_open_for_registrations")] bool? OpenRegistrations);

public sealed record FediverseInstanceListItem(
    Topic Topic,
    FediverseInstanceAttributes? Instance,
    TopicElection? TopicElection,
    HostnameElection? HostnameElection);

public sealed record FediverseInstancesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("topics")] IReadOnlyDictionary<string, Topic> Topics,
    [property: JsonPropertyName("fediverse_instances")] IReadOnlyDictionary<string, FediverseInstanceAttributes> Instances,
    [property: JsonPropertyName("topic_elections")] IReadOnlyDictionary<string, TopicElection> TopicElections,
    [property: JsonPropertyName("hostname_elections")] IReadOnlyDictionary<string, HostnameElection> HostnameElections,
    [property: JsonPropertyName("topics_metrics")] IReadOnlyDictionary<string, object>? TopicsMetrics = null,
    [property: JsonPropertyName("markdown_to_html")] IReadOnlyDictionary<string, string>? MarkdownToHtml = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, ElectionVote>? ElectionVotes = null)
{
  public IReadOnlyList<FediverseInstanceListItem> OrderedInstances => Results
      .Select(result => result.Id)
      .Where(id => id is not null && Topics.ContainsKey(id))
      .Select(id =>
      {
        var topic = Topics[id!];
        return new FediverseInstanceListItem(
            topic,
            Instances.GetValueOrDefault(id!),
            TopicElections.GetValueOrDefault(id!),
            topic.HostnameId is { } hostnameId ? HostnameElections.GetValueOrDefault(hostnameId) : null);
      })
      .ToArray();
}

public sealed record FediverseInstanceResponse(
    [property: JsonPropertyName("topic")] Topic Topic,
    [property: JsonPropertyName("fediverse_instance")] FediverseInstanceAttributes? Instance,
    [property: JsonPropertyName("topic_election")] TopicElection? TopicElection,
    [property: JsonPropertyName("hostname_election")] HostnameElection? HostnameElection);
