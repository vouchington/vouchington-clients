using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056

public sealed record UpdateMyIdentityBody(
    [property: JsonPropertyName("username")] string? Username = null,
    [property: JsonPropertyName("use_display_name_from")] string? UseDisplayNameFrom = null,
    [property: JsonPropertyName("profile_image_id")] JsonNullableString? ProfileImageId = null);

public sealed record UpdateUserPrivacyBody(
    [property: JsonPropertyName("follows_visibility")] string? FollowsVisibility = null,
    [property: JsonPropertyName("topic_follows_visibility")] string? TopicFollowsVisibility = null,
    [property: JsonPropertyName("rss_feed_follows_visibility")] string? RssFeedFollowsVisibility = null,
    [property: JsonPropertyName("community_memberships_visibility")] string? CommunityMembershipsVisibility = null,
    [property: JsonPropertyName("followers_visibility")] string? FollowersVisibility = null,
    [property: JsonPropertyName("likes_visibility")] string? LikesVisibility = null,
    [property: JsonPropertyName("direct_messages_audience")] string? DirectMessagesAudience = null,
    [property: JsonPropertyName("cards_visibility")] string? CardsVisibility = null,
    [property: JsonPropertyName("rewards_program_statuses_visibility")] string? RewardsProgramStatusesVisibility = null,
    [property: JsonPropertyName("spending_categories_visibility")] string? SpendingCategoriesVisibility = null,
    [property: JsonPropertyName("default_post_broadcast")] string? DefaultPostBroadcast = null,
    [property: JsonPropertyName("default_post_privacy")] string? DefaultPostPrivacy = null,
    [property: JsonPropertyName("ui_locale")] JsonNullableString? UiLocale = null,
    [property: JsonPropertyName("processing_restricted_at")] bool? ProcessingRestrictedAt = null,
    [property: JsonPropertyName("third_party_marketing")] bool? ThirdPartyMarketing = null,
    [property: JsonPropertyName("hn_discussions")] bool? HnDiscussions = null);

public sealed record UpdateEmailPreferencesBody(
    [property: JsonPropertyName("is_engagement_emails_enabled")] bool? EngagementEmailsEnabled = null,
    [property: JsonPropertyName("news_digest_frequency")] string? NewsDigestFrequency = null,
    [property: JsonPropertyName("is_moderation_emails_enabled")] bool? ModerationEmailsEnabled = null,
    [property: JsonPropertyName("community_digest_frequency")] string? CommunityDigestFrequency = null,
    [property: JsonPropertyName("moderation_email_cadence")] string? ModerationEmailCadence = null,
    [property: JsonPropertyName("moderation_email_days_of_week")] IReadOnlyList<int>? ModerationEmailDaysOfWeek = null,
    [property: JsonPropertyName("moderation_email_time_of_day")] string? ModerationEmailTimeOfDay = null,
    [property: JsonPropertyName("moderation_email_timezone")] string? ModerationEmailTimezone = null);

public sealed record CreateApiKeyBody(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("permissions")] IReadOnlyList<string> Permissions,
    [property: JsonPropertyName("type")] string Type = "rss",
    // A missing property keeps the server default; a present JSON null requests no expiry.
    [property: JsonPropertyName("lifetime_days"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? LifetimeDays = null);

public sealed record CreateProfileLinkBody(
    [property: JsonPropertyName("link_type")] string LinkType,
    [property: JsonPropertyName("url")] string? Url = null,
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("image_id")] string? ImageId = null);

public sealed record UpdateProfileLinkBody(
    [property: JsonPropertyName("url")] string? Url = null,
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("image_id")] string? ImageId = null);

public sealed record ReorderProfileLinksBody([property: JsonPropertyName("ids")] IReadOnlyList<string> Ids);

public sealed record MembershipCheckoutBody(
    [property: JsonPropertyName("price_id")] string PriceId,
    [property: JsonPropertyName("success_url")] Uri SuccessUrl,
    [property: JsonPropertyName("cancel_url")] Uri CancelUrl);

public sealed record MembershipPortalBody(
    [property: JsonPropertyName("return_url")] Uri ReturnUrl);

#pragma warning restore CA1054, CA1056
