using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

/// <summary>
/// A notification row. Copy is rendered client-side from <see cref="Kind"/>, <see cref="Params"/>
/// and the list response hydration maps; <see cref="Kind"/> stays a string so an unknown future
/// kind decodes and renders as a generic row.
/// </summary>
public sealed record Notification(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("entity_type")] string? EntityType,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("read_at")] DateTimeOffset? ReadAt,
    [property: JsonPropertyName("target_path")] string? TargetPath,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("rss_feed_item_id")] string? RssFeedItemId,
    [property: JsonPropertyName("conversation_id")] string? ConversationId,
    [property: JsonPropertyName("community_id")] string? CommunityId = null,
    [property: JsonPropertyName("event_key")] string? EventKey = null,
    [property: JsonPropertyName("target_entity")] NotificationTargetEntity? TargetEntity = null,
    [property: JsonPropertyName("target_intent")] string? TargetIntent = null,
    [property: JsonPropertyName("user_id")] string? UserId = null,
    [property: JsonPropertyName("actor_user_id")] string? ActorUserId = null,
    [property: JsonPropertyName("__entity_type")] string? ResponseEntityType = null,
    [property: JsonPropertyName("moderation_report_id")] string? ModerationReportId = null,
    [property: JsonPropertyName("pushed_at")] DateTimeOffset? PushedAt = null,
    [property: JsonPropertyName("review_dispute_id")] string? ReviewDisputeId = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("user_warning_id")] string? UserWarningId = null,
    [property: JsonPropertyName("copyright_notice_id")] string? CopyrightNoticeId = null,
    [property: JsonPropertyName("household_id")] string? HouseholdId = null,
    [property: JsonPropertyName("kind")] string? Kind = null,
    [property: JsonPropertyName("params")] IReadOnlyDictionary<string, JsonElement>? Params = null,
    [property: JsonPropertyName("sent_by_id")] string? SentById = null,
    [property: JsonPropertyName("moderation_appeal_id")] string? ModerationAppealId = null,
    [property: JsonPropertyName("community_application_id")] string? CommunityApplicationId = null,
    [property: JsonPropertyName("copyright_notice_delivery_work_item_id")] string? CopyrightNoticeDeliveryWorkItemId = null,
    [property: JsonPropertyName("top_post_id")] string? TopPostId = null);

public sealed record NotificationsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("notifications")] IReadOnlyDictionary<string, Notification> Notifications,
    [property: JsonPropertyName("communities")] IReadOnlyDictionary<string, NotificationCommunity>? Communities = null,
    [property: JsonPropertyName("actors")] IReadOnlyDictionary<string, NotificationActor>? Actors = null,
    [property: JsonPropertyName("posts")] IReadOnlyDictionary<string, NotificationPost>? Posts = null,
    [property: JsonPropertyName("rss_feed_items")] IReadOnlyDictionary<string, NotificationRssFeedItem>? RssFeedItems = null,
    [property: JsonPropertyName("copyright_notices")] IReadOnlyDictionary<string, NotificationCopyrightNotice>? CopyrightNotices = null,
    [property: JsonPropertyName("public_texts")] IReadOnlyDictionary<string, string>? PublicTexts = null);

public sealed record NotificationTargetEntity(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("id")] string Id);

public sealed record NotificationCommunity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name);

public sealed record NotificationActor(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username);

public sealed record NotificationPost(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("post_type")] string? PostType,
    [property: JsonPropertyName("snippet")] string? Snippet,
    [property: JsonPropertyName("actor_user_id")] string? ActorUserId,
    [property: JsonPropertyName("community_id")] string? CommunityId);

public sealed record NotificationRssFeedItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("feed_name")] string? FeedName,
    [property: JsonPropertyName("snippet")] string? Snippet);

public sealed record NotificationCopyrightNotice(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("jurisdiction")] string? Jurisdiction);

public sealed record FetchNotificationsRequest(
    string? After = null,
    int Limit = 20);

public sealed record NotificationRedirectTargetResponse(
    [property: JsonPropertyName("target_url")] string TargetUrl);
