using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record Hostname(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("hostname")] string HostnameValue,
    [property: JsonPropertyName("topic_id")] string? TopicId = null,
    [property: JsonPropertyName("blocked")] bool? Blocked = null,
    [property: JsonPropertyName("crawlable")] bool? Crawlable = null,
    [property: JsonPropertyName("skip_web_risk")] bool? SkipWebRisk = null,
    [property: JsonPropertyName("link_rel_follow")] bool? LinkRelFollow = null,
    [property: JsonPropertyName("votes_score_net")] double? VotesScoreNet = null,
    [property: JsonPropertyName("votes_count_up")] int? VotesCountUp = null,
    [property: JsonPropertyName("votes_count_down")] int? VotesCountDown = null);

public sealed record HostnameElection(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("votes_score_net")] double VotesScoreNet,
    [property: JsonPropertyName("votes_count_up")] int VotesCountUp,
    [property: JsonPropertyName("votes_count_down")] int VotesCountDown);

public sealed record HostnameTopUrl(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("pathname")] string Pathname,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("canonical_url_id")] string? CanonicalUrlId = null,
    [property: JsonPropertyName("hostname")] Hostname? Hostname = null,
    [property: JsonPropertyName("search_params")] IReadOnlyDictionary<string, string>? SearchParams = null);

public sealed record HostnamesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("hostnames")] IReadOnlyDictionary<string, Hostname> Hostnames,
    [property: JsonPropertyName("topics")] IReadOnlyDictionary<string, Topic>? Topics = null,
    [property: JsonPropertyName("hostname_elections")] IReadOnlyDictionary<string, HostnameElection>? HostnameElections = null,
    [property: JsonPropertyName("top_urls_by_hostname_id")] IReadOnlyDictionary<string, IReadOnlyList<HostnameTopUrl>>? TopUrlsByHostnameId = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, ElectionVote>? ElectionVotes = null);

public sealed record HostnameCollectionResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<Hostname> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record HostnameDetailResponse(
    [property: JsonPropertyName("hostname")] Hostname Hostname,
    [property: JsonPropertyName("topic")] Topic? Topic,
    [property: JsonPropertyName("top_urls")] IReadOnlyList<HostnameTopUrl> TopUrls,
    [property: JsonPropertyName("rss_feeds")] IReadOnlyList<RssFeedSource> RssFeeds,
    [property: JsonPropertyName("hostname_election")] HostnameElection? HostnameElection,
    [property: JsonPropertyName("election_vote")] ElectionVote? ElectionVote,
    [property: JsonPropertyName("crawlers")] IReadOnlyList<object>? Crawlers = null);

#pragma warning restore CA1054, CA1056, CA1720
