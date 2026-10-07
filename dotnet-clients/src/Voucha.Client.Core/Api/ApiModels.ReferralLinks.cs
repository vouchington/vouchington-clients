using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record ReferralLinkFeedUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("display_name")] string? DisplayName = null,
    [property: JsonPropertyName("profile_image_id")] string? ProfileImageId = null,
    [property: JsonPropertyName("account_type")] AccountType? AccountType = null);

public sealed record ReferralLinkFeedItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("referral_program_id")] string ReferralProgramId,
    [property: JsonPropertyName("referral_program_name")] string ReferralProgramName,
    [property: JsonPropertyName("referral_program_slug")] string ReferralProgramSlug,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("label")] string? Label);

public sealed record ReferralLinkFeedResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ReferralLinkFeedItem> Results,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, ReferralLinkFeedUser> Users,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ReferralLink(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("referral_program_id")] string ReferralProgramId,
    [property: JsonPropertyName("url_id")] string UrlId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("activated_at")] DateTimeOffset? ActivatedAt,
    [property: JsonPropertyName("deactivated_at")] DateTimeOffset? DeactivatedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("referral_program_name")] string ReferralProgramName,
    [property: JsonPropertyName("referral_program_slug")] string ReferralProgramSlug,
    [property: JsonPropertyName("consecutive_crawl_failures")] int? ConsecutiveCrawlFailures = null,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null,
    [property: JsonPropertyName("last_crawl_failure_at")] DateTimeOffset? LastCrawlFailureAt = null,
    [property: JsonPropertyName("last_crawl_id")] string? LastCrawlId = null,
    [property: JsonPropertyName("last_crawl_success_at")] DateTimeOffset? LastCrawlSuccessAt = null,
    [property: JsonPropertyName("parent_link_id")] string? ParentLinkId = null,
    [property: JsonPropertyName("unfurl_requested_at")] DateTimeOffset? UnfurlRequestedAt = null,
    [property: JsonPropertyName("unfurl_completed_at")] DateTimeOffset? UnfurlCompletedAt = null,
    [property: JsonPropertyName("unfurl_failed_at")] DateTimeOffset? UnfurlFailedAt = null,
    [property: JsonPropertyName("unfurl_last_error")] string? UnfurlLastError = null);

public sealed record ReferralLinksResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ReferralLink> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ReferralClickLogResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record ReferralClickLogEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("landing_url")] string LandingUrl,
    [property: JsonPropertyName("signed_up_at")] DateTimeOffset? SignedUpAt,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record ReferralClickLogResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ReferralClickLogResult> Results,
    [property: JsonPropertyName("clicks")] IReadOnlyDictionary<string, ReferralClickLogEntry> Clicks,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, User> Users,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record TrendingReferralProgram(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("trending_score")] double? TrendingScore = null,
    [property: JsonPropertyName("link_count")] int? LinkCount = null);

public sealed record TrendingReferralProgramsResponse(
    [property: JsonPropertyName("referral_programs")] IReadOnlyList<TrendingReferralProgram> ReferralPrograms,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo)
{
  [JsonIgnore]
  public IReadOnlyList<TrendingReferralProgram> Results => ReferralPrograms;
}

public sealed record PrioritizedReferralLink(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("is_official")] bool IsOfficial,
    [property: JsonPropertyName("referral_program_id")] string ReferralProgramId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("priority_group")] int PriorityGroup,
    [property: JsonPropertyName("contribution_rank")] int ContributionRank,
    [property: JsonPropertyName("tier_rank")] int TierRank,
    [property: JsonPropertyName("best_score")] double BestScore,
    [property: JsonPropertyName("review_post_id")] string? ReviewPostId,
    [property: JsonPropertyName("review_post_slug")] string? ReviewPostSlug,
    [property: JsonPropertyName("review_avg_rating")] double? ReviewAvgRating);

public sealed record PrioritizedReferralLinksResponse(
    [property: JsonPropertyName("links")] IReadOnlyList<PrioritizedReferralLink> Links,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, ReferralLinkFeedUser> Users);

#pragma warning restore CA1054, CA1056, CA1720
