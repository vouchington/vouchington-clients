using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record TopicHostname(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("hostname")] string Hostname,
    [property: JsonPropertyName("topic_id")] string? TopicId);

public sealed record TopicImagePlacement(
    [property: JsonPropertyName("image_id")] string ImageId,
    [property: JsonPropertyName("placement_id")] string PlacementId,
    [property: JsonPropertyName("placement_revision")] int PlacementRevision);

public sealed record Topic(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("topic_type")] string TopicType,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("html")] string? Html = null,
    [property: JsonPropertyName("aliases")] IReadOnlyList<string>? Aliases = null,
    [property: JsonPropertyName("should_allow_reviews")] bool? AllowReviews = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("created_by")] User? CreatedBy = null,
    [property: JsonPropertyName("hero_image_id")] string? HeroImageId = null,
    [property: JsonPropertyName("homepage_url_id")] string? HomepageUrlId = null,
    [property: JsonPropertyName("hostname")] TopicHostname? Hostname = null,
    [property: JsonPropertyName("hostname_id")] string? HostnameId = null,
    [property: JsonPropertyName("logo_image_id")] string? LogoImageId = null,
    [property: JsonPropertyName("is_noindexed")] bool? Noindex = null,
    [property: JsonPropertyName("referral_program_id")] string? ReferralProgramId = null,
    [property: JsonPropertyName("referral_program_slug")] string? ReferralProgramSlug = null,
    [property: JsonPropertyName("rewards_program_id")] string? RewardsProgramId = null,
    [property: JsonPropertyName("updated_by")] User? UpdatedBy = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null,
    [property: JsonPropertyName("hero_image_placement")] TopicImagePlacement? HeroImagePlacement = null,
    [property: JsonPropertyName("logo_image_placement")] TopicImagePlacement? LogoImagePlacement = null);

public sealed record TopicSearchResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("topics")] IReadOnlyDictionary<string, Topic> Topics,
    [property: JsonPropertyName("topics_metrics")] IReadOnlyDictionary<string, object> TopicsMetrics,
    [property: JsonPropertyName("topic_elections")] IReadOnlyDictionary<string, TopicElection>? TopicElections = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, ElectionVote>? ElectionVotes = null);

public sealed record TopicMutationResponse([property: JsonPropertyName("topic")] Topic Topic);

public sealed record TopicAlias(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("topic_id")] string TopicId);

public sealed record TopicMergeResponse(
    [property: JsonPropertyName("topic")] Topic Topic,
    [property: JsonPropertyName("topic_merge")] TopicMergeDetails? TopicMerge = null);

public sealed record TopicMergeDetails(
    [property: JsonPropertyName("source_topic_id")] string SourceTopicId,
    [property: JsonPropertyName("destination_topic_id")] string DestinationTopicId,
    [property: JsonPropertyName("moved_aliases")] IReadOnlyList<string> MovedAliases);

public sealed record TopicAdditionalHostname(
    [property: JsonPropertyName("hostname_id")] string HostnameId,
    [property: JsonPropertyName("hostname")] string Hostname,
    [property: JsonPropertyName("topic_id")] string TopicId,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null);

public sealed record TopicAdditionalHostnameResponse(
    [property: JsonPropertyName("additional_hostname")] TopicAdditionalHostname AdditionalHostname);

#pragma warning restore CA1054, CA1056, CA1720
