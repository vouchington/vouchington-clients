using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056

public sealed record UserResponse(
    [property: JsonPropertyName("user")] User User,
    [property: JsonPropertyName("user_metrics")] UserMetrics? UserMetrics = null,
    [property: JsonPropertyName("profile_links")] IReadOnlyList<ProfileLink>? ProfileLinks = null,
    [property: JsonPropertyName("user_bio_html")] string? UserBioHtml = null);

public sealed record EmailPreferences(
    [property: JsonPropertyName("is_engagement_emails_enabled")] bool EngagementEmailsEnabled,
    [property: JsonPropertyName("news_digest_frequency")] string NewsDigestFrequency,
    [property: JsonPropertyName("is_moderation_emails_enabled")] bool ModerationEmailsEnabled,
    [property: JsonPropertyName("community_digest_frequency")] string CommunityDigestFrequency,
    [property: JsonPropertyName("moderation_email_cadence")] string ModerationEmailCadence,
    [property: JsonPropertyName("moderation_email_days_of_week")] IReadOnlyList<int> ModerationEmailDaysOfWeek,
    [property: JsonPropertyName("moderation_email_time_of_day")] string ModerationEmailTimeOfDay,
    [property: JsonPropertyName("moderation_email_timezone")] string? ModerationEmailTimezone);

public sealed record EmailPreferencesResponse(
    [property: JsonPropertyName("email_preferences")] EmailPreferences EmailPreferences);

public sealed record UserMetricsCount(
    [property: JsonPropertyName("reviews")] int Reviews = 0,
    [property: JsonPropertyName("discussions")] int Discussions = 0,
    [property: JsonPropertyName("comments")] int Comments = 0,
    [property: JsonPropertyName("users_following")] int UsersFollowing = 0,
    [property: JsonPropertyName("users_followers")] int UsersFollowers = 0,
    [property: JsonPropertyName("topics_following")] int TopicsFollowing = 0,
    [property: JsonPropertyName("rss_feeds_following")] int RssFeedsFollowing = 0,
    [property: JsonPropertyName("communities_member")] int CommunitiesMember = 0);

public sealed record UserMetricsViewerCount(
    [property: JsonPropertyName("reviews")] int? Reviews = null,
    [property: JsonPropertyName("discussions")] int? Discussions = null,
    [property: JsonPropertyName("comments")] int? Comments = null);

public sealed record UserMetrics(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("count")] UserMetricsCount Count,
    [property: JsonPropertyName("viewer_count")] UserMetricsViewerCount? ViewerCount = null,
    [property: JsonPropertyName("bookmarkers")] IReadOnlyDictionary<string, int>? Bookmarkers = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? Bookmarks = null,
    [property: JsonPropertyName("bookmarks__updated_at")] DateTimeOffset? BookmarksUpdatedAt = null);

public sealed record UserDataRequestResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("download_url")] Uri? DownloadUrl = null,
    [property: JsonPropertyName("queued_at")] DateTimeOffset? QueuedAt = null,
    [property: JsonPropertyName("processing_started_at")] DateTimeOffset? ProcessingStartedAt = null,
    [property: JsonPropertyName("completed_at")] DateTimeOffset? CompletedAt = null,
    [property: JsonPropertyName("failed_at")] DateTimeOffset? FailedAt = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null);

public sealed record UserDataRequestCreationResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt);

public sealed record ApiKey(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("prefix")] string Prefix,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("permissions")] IReadOnlyList<string> Permissions,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("last_used_at")] DateTimeOffset? LastUsedAt,
    [property: JsonPropertyName("revoked_at")] DateTimeOffset? RevokedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt = null,
    [property: JsonPropertyName("expiry_reminder_sent_at")] DateTimeOffset? ExpiryReminderSentAt = null,
    [property: JsonPropertyName("replaced_by_api_key_id")] string? ReplacedByApiKeyId = null);

public sealed record ApiKeyListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ApiKey> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ApiKeyCreationResponse(
    [property: JsonPropertyName("api_key")] ApiKey ApiKey,
    [property: JsonPropertyName("raw_key")] string RawKey);

public sealed record ProfileLink(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("link_type")] string LinkType,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("url_id")] string? UrlId,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("image_id")] string? ImageId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("image_placement")] TopicImagePlacement? ImagePlacement = null);

public sealed record ProfileLinkListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ProfileLink> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ProfileLinkResponse(
    [property: JsonPropertyName("profile_link")] ProfileLink ProfileLink);

public sealed record WebPushSubscription(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("endpoint")] string Endpoint,
    [property: JsonPropertyName("p256dh")] string P256dh,
    [property: JsonPropertyName("auth")] string Auth,
    [property: JsonPropertyName("expiration_time_ms")] string? ExpirationTimeMs,
    [property: JsonPropertyName("user_agent")] string UserAgent,
    [property: JsonPropertyName("last_success_at")] DateTimeOffset? LastSuccessAt,
    [property: JsonPropertyName("last_failure_at")] DateTimeOffset? LastFailureAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record WebPushSubscriptionListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<WebPushSubscription> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record AuthSession(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("device_id")] string DeviceId,
    [property: JsonPropertyName("device_name")] string? DeviceName,
    [property: JsonPropertyName("user_agent")] string? UserAgent,
    [property: JsonPropertyName("ip_address")] string? IpAddress,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("last_seen_at")] DateTimeOffset? LastSeenAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("is_current")] bool IsCurrent);

public sealed record AuthSessionListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<AuthSession> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record DeleteUserResponse([property: JsonPropertyName("logout")] bool Logout);

#pragma warning restore CA1054, CA1056
