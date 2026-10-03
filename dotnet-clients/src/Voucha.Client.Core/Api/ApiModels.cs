using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record PageInfo(
    [property: JsonPropertyName("end_cursor")] string? EndCursor,
    [property: JsonPropertyName("has_next_page")] bool HasNextPage,
    [property: JsonPropertyName("start_cursor")] string? StartCursor,
    [property: JsonPropertyName("has_more")] bool? HasMore = null,
    [property: JsonPropertyName("has_previous_page")] bool? HasPreviousPage = null);

public sealed record EntityReference(
    [property: JsonPropertyName("__entity_type")] string? EntityType,
    [property: JsonPropertyName("entity_id")] string? EntityId,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("topic_type")] string? TopicType,
    [property: JsonPropertyName("read_at")] DateTimeOffset? ReadAt,
    [property: JsonPropertyName("delivery_type")] string? DeliveryType,
    [property: JsonPropertyName("shared_by_user_id")] string? SharedByUserId,
    [property: JsonPropertyName("shared_at")] DateTimeOffset? SharedAt,
    [property: JsonPropertyName("story_id")] string? StoryId,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt = null);

public sealed record User(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("display_name_source")] string? DisplayNameSource = null,
    [property: JsonPropertyName("profile_image_id")] string? ProfileImageId = null,
    [property: JsonPropertyName("email_address")] string? EmailAddress = null,
    [property: JsonPropertyName("use_display_name_from")] string? UseDisplayNameFrom = null,
    [property: JsonPropertyName("cards_visibility")] string? CardsVisibility = null,
    [property: JsonPropertyName("rewards_program_statuses_visibility")] string? RewardsProgramStatusesVisibility = null,
    [property: JsonPropertyName("spending_categories_visibility")] string? SpendingCategoriesVisibility = null,
    [property: JsonPropertyName("follows_visibility")] string? FollowsVisibility = null,
    [property: JsonPropertyName("topic_follows_visibility")] string? TopicFollowsVisibility = null,
    [property: JsonPropertyName("rss_feed_follows_visibility")] string? RssFeedFollowsVisibility = null,
    [property: JsonPropertyName("community_memberships_visibility")] string? CommunityMembershipsVisibility = null,
    [property: JsonPropertyName("followers_visibility")] string? FollowersVisibility = null,
    [property: JsonPropertyName("likes_visibility")] string? LikesVisibility = null,
    [property: JsonPropertyName("direct_messages_audience")] string? DirectMessagesAudience = null,
    [property: JsonPropertyName("default_post_broadcast")] string? DefaultPostBroadcast = null,
    [property: JsonPropertyName("default_post_privacy")] string? DefaultPostPrivacy = null,
    [property: JsonPropertyName("is_engagement_emails_enabled")] bool? EngagementEmailsEnabled = null,
    [property: JsonPropertyName("news_digest_frequency")] string? NewsDigestFrequency = null,
    [property: JsonPropertyName("is_moderation_emails_enabled")] bool? ModerationEmailsEnabled = null,
    [property: JsonPropertyName("community_digest_frequency")] string? CommunityDigestFrequency = null,
    [property: JsonPropertyName("moderation_email_cadence")] string? ModerationEmailCadence = null,
    [property: JsonPropertyName("moderation_email_days_of_week")] IReadOnlyList<int>? ModerationEmailDaysOfWeek = null,
    [property: JsonPropertyName("moderation_email_time_of_day")] string? ModerationEmailTimeOfDay = null,
    [property: JsonPropertyName("moderation_email_timezone")] string? ModerationEmailTimezone = null,
    [property: JsonPropertyName("is_fediverse_federation_enabled")] bool? FediverseFederationEnabled = null,
    [property: JsonPropertyName("processing_restricted_at")] DateTimeOffset? ProcessingRestrictedAt = null,
    [property: JsonPropertyName("third_party_marketing")] bool? ThirdPartyMarketing = null,
    [property: JsonPropertyName("hn_discussions")] bool? HnDiscussions = null,
    [property: JsonPropertyName("roles")] IReadOnlyList<string>? Roles = null,
    [property: JsonPropertyName("account_type")] AccountType? AccountType = null,
    [property: JsonPropertyName("ui_locale")] string? UiLocale = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("verified_display_name")] string? VerifiedDisplayName = null,
    [property: JsonPropertyName("display_account")] PublicDisplayAccount? DisplayAccount = null,
    [property: JsonPropertyName("facebook_account")] UserDisplayAccount? FacebookAccount = null,
    [property: JsonPropertyName("apple_account")] UserDisplayAccount? AppleAccount = null,
    [property: JsonPropertyName("google_account")] UserDisplayAccount? GoogleAccount = null,
    [property: JsonPropertyName("x_account")] UserDisplayAccount? XAccount = null,
    [property: JsonPropertyName("linkedin_account")] UserDisplayAccount? LinkedinAccount = null,
    [property: JsonPropertyName("microsoft_account")] UserDisplayAccount? MicrosoftAccount = null,
    [property: JsonPropertyName("github_account")] UserDisplayAccount? GithubAccount = null,
    [property: JsonPropertyName("bluesky_account")] BlueskyAccount? BlueskyAccount = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("membership_plan")] string? MembershipPlan = null,
    [property: JsonPropertyName("suspended_at")] DateTimeOffset? SuspendedAt = null,
    [property: JsonPropertyName("verification_status")] string? VerificationStatus = null,
    [property: JsonPropertyName("profile_image_placement")] TopicImagePlacement? ProfileImagePlacement = null,
    [property: JsonPropertyName("is_verified_badge_visible")] bool? IsVerifiedBadgeVisible = null);

public sealed record PublicDisplayAccount(
    [property: JsonPropertyName("name")] string? Name);

public sealed record UserDisplayAccount(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name);

// AT Protocol identity has no numeric provider ID or email — the DID is the durable identifier,
// with handle as the mutable human-readable label — so it does not fit UserDisplayAccount's shape.
public sealed record BlueskyAccount(
    [property: JsonPropertyName("did")] string Did,
    [property: JsonPropertyName("handle")] string? Handle);

public sealed record Community(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("markdown")] string? Markdown,
    [property: JsonPropertyName("visibility")] string Visibility,
    [property: JsonPropertyName("member_roster_visibility")] string MemberRosterVisibility,
    [property: JsonPropertyName("list_type")] string? ListType,
    [property: JsonPropertyName("member_invites_allowed_at")] DateTimeOffset? MemberInvitesAllowedAt,
    [property: JsonPropertyName("post_approval_required_at")] DateTimeOffset? PostApprovalRequiredAt,
    [property: JsonPropertyName("should_allow_review_posts")] bool AllowReviewPosts,
    [property: JsonPropertyName("should_allow_data_point_posts")] bool AllowDataPointPosts,
    [property: JsonPropertyName("created_by_id")] string CreatedById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("trusted_at")] DateTimeOffset? TrustedAt = null,
    [property: JsonPropertyName("profile_image_id")] string? ProfileImageId = null,
    [property: JsonPropertyName("banner_image_id")] string? BannerImageId = null,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById = null,
    [property: JsonPropertyName("archived_at")] DateTimeOffset? ArchivedAt = null,
    [property: JsonPropertyName("archived_by_id")] string? ArchivedById = null,
    [property: JsonPropertyName("default_language")] string? DefaultLanguage = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null,
    [property: JsonPropertyName("rules_markdown")] string? RulesMarkdown = null,
    [property: JsonPropertyName("owner")] User? Owner = null,
    [property: JsonPropertyName("automod_action")] string? AutomodAction = null,
    [property: JsonPropertyName("banner_image_placement")] TopicImagePlacement? BannerImagePlacement = null,
    [property: JsonPropertyName("profile_image_placement")] TopicImagePlacement? ProfileImagePlacement = null,
    [property: JsonPropertyName("content_provenance")] PublicContentProvenance? ContentProvenance = null);

public sealed record CommunityMetrics(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("member_count")] int MemberCount,
    [property: JsonPropertyName("post_count")] int PostCount,
    [property: JsonPropertyName("list_item_count")] int ListItemCount = 0,
    [property: JsonPropertyName("proxy_follow_count")] int ProxyFollowCount = 0,
    [property: JsonPropertyName("proxy_mute_count")] int ProxyMuteCount = 0,
    [property: JsonPropertyName("virtual_subscription_count")] int VirtualSubscriptionCount = 0,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CommunitySearchResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("communities")] IReadOnlyDictionary<string, Community> Communities,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, CommunityOwner> Users,
    [property: JsonPropertyName("community_metrics")] IReadOnlyDictionary<string, CommunityMetrics> CommunityMetrics,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null,
    [property: JsonPropertyName("community_memberships")] IReadOnlyDictionary<string, CommunityMember>? CommunityMemberships = null,
    [property: JsonPropertyName("pending_application_community_ids")] IReadOnlyList<string>? PendingApplicationCommunityIds = null);

public sealed record CommunityResponse(
    [property: JsonPropertyName("community")] Community Community,
    [property: JsonPropertyName("user")] CommunityOwner? User,
    [property: JsonPropertyName("community_metrics")] CommunityMetrics CommunityMetrics,
    [property: JsonPropertyName("membership")] CommunityMember? Membership,
    [property: JsonPropertyName("has_pending_application")] bool? HasPendingApplication);

public sealed record CommunityMutationResponse(
    [property: JsonPropertyName("community")] Community Community);

public sealed record CommunityOwner(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username = null,
    [property: JsonPropertyName("account_type")] AccountType? AccountType = null);

public sealed record PublicUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("profile_image_id")] string? ProfileImageId = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("use_display_name_from")] string? UseDisplayNameFrom = null,
    [property: JsonPropertyName("account_type")] AccountType? AccountType = null,
    [property: JsonPropertyName("verification_status")] string? VerificationStatus = null,
    [property: JsonPropertyName("is_verified_badge_visible")] bool? VerifiedBadgeVisible = null,
    [property: JsonPropertyName("verified_display_name")] string? VerifiedDisplayName = null,
    [property: JsonPropertyName("public_verified_name_display")] string? PublicVerifiedNameDisplay = null,
    [property: JsonPropertyName("roles")] IReadOnlyList<string>? Roles = null,
    [property: JsonPropertyName("display_account")] PublicDisplayAccount? DisplayAccount = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

#pragma warning restore CA1054, CA1056, CA1720
