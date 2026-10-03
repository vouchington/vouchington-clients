using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record Post(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("post_type")] string? PostType,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("markdown")] string? Markdown,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("broadcast")] string? Broadcast = null,
    [property: JsonPropertyName("clearance_status")] string? ClearanceStatus = null,
    [property: JsonPropertyName("community_id")] string? CommunityId = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("html")] string? Html = null,
    [property: JsonPropertyName("is_anonymous")] bool? IsAnonymous = null,
    [property: JsonPropertyName("parent_post_id")] string? ParentId = null,
    [property: JsonPropertyName("privacy")] string? Privacy = null,
    [property: JsonPropertyName("root_post_id")] string? RootId = null,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("created_by")] User? CreatedBy = null,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById = null,
    [property: JsonPropertyName("locked_at")] DateTimeOffset? LockedAt = null,
    [property: JsonPropertyName("locked_by_id")] string? LockedById = null,
    [property: JsonPropertyName("can_edit_content")] bool? CanEditContent = null,
    [property: JsonPropertyName("can_delete")] bool? CanDelete = null,
    [property: JsonPropertyName("can_lock")] bool? CanLock = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("ai_summary_markdown")] string? AiSummaryMarkdown = null,
    [property: JsonPropertyName("archived_at")] DateTimeOffset? ArchivedAt = null,
    [property: JsonPropertyName("archived_by_id")] string? ArchivedById = null,
    [property: JsonPropertyName("clearance_reason")] string? ClearanceReason = null,
    [property: JsonPropertyName("clearance_updated_at")] DateTimeOffset? ClearanceUpdatedAt = null,
    [property: JsonPropertyName("spam_detection_created_at")] DateTimeOffset? SpamDetectionCreatedAt = null,
    [property: JsonPropertyName("spam_detection_flagged")] bool? SpamDetectionFlagged = null,
    [property: JsonPropertyName("spam_detection_results")] object? SpamDetectionResults = null,
    [property: JsonPropertyName("spam_detection_score")] double? SpamDetectionScore = null,
    [property: JsonPropertyName("updated_by_id")] string? UpdatedById = null,
    [property: JsonPropertyName("url")] Uri? Url = null,
    [property: JsonPropertyName("url_id")] string? UrlId = null,
    [property: JsonPropertyName("user_id")] string? UserId = null,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt = null,
    [property: JsonPropertyName("in_review_at")] DateTimeOffset? InReviewAt = null,
    [property: JsonPropertyName("rejected_at")] DateTimeOffset? RejectedAt = null,
    [property: JsonPropertyName("post_explicit_categories")] IReadOnlyList<PostExplicitCategory>? PostExplicitCategories = null,
    [property: JsonPropertyName("post_hashtags")] IReadOnlyList<PostHashtag>? PostHashtags = null,
    [property: JsonPropertyName("declared_language")] string? DeclaredLanguage = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null,
    [property: JsonPropertyName("content_provenance")] PublicContentProvenance? ContentProvenance = null);

public sealed record PublicContentProvenance(
    [property: JsonPropertyName("via")] string Via,
    [property: JsonPropertyName("label")] string Label);

public sealed record PostExplicitCategory(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("topic_id")] string? TopicId = null,
    [property: JsonPropertyName("topic_name")] string? TopicName = null,
    [property: JsonPropertyName("hashtag")] string? Hashtag = null);

public sealed record PostHashtag(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("display_token")] string DisplayToken,
    [property: JsonPropertyName("topic_id")] string? TopicId);

public sealed record PostMetricCounts(
    [property: JsonPropertyName("descendants")] int Descendants,
    [property: JsonPropertyName("children")] int Children = 0,
    [property: JsonPropertyName("ancestors")] int Ancestors = 0);

public sealed record PostMetrics(
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("count")] PostMetricCounts Count = null!,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, int>? Bookmarks = null);

public sealed record PostsFeedResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("posts")] IReadOnlyDictionary<string, Post> Posts,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, User> Users,
    [property: JsonPropertyName("communities")] IReadOnlyDictionary<string, Community> Communities,
    [property: JsonPropertyName("post_elections")] IReadOnlyDictionary<string, PostElection>? PostElections = null,
    [property: JsonPropertyName("markdown_to_html")] IReadOnlyDictionary<string, string>? MarkdownToHtml = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, ElectionVote>? ElectionVotes = null,
    [property: JsonPropertyName("posts_metrics")] IReadOnlyDictionary<string, PostMetrics>? PostsMetrics = null,
    [property: JsonPropertyName("post_link_embeds")] IReadOnlyDictionary<string, UrlEmbed>? PostLinkEmbeds = null);

public sealed record PostThreadResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("posts")] IReadOnlyDictionary<string, Post> Posts,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, User>? Users = null,
    [property: JsonPropertyName("communities")] IReadOnlyDictionary<string, Community>? Communities = null,
    [property: JsonPropertyName("posts_metrics")] IReadOnlyDictionary<string, PostMetrics>? PostsMetrics = null,
    [property: JsonPropertyName("post_elections")] IReadOnlyDictionary<string, PostElection>? PostElections = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, ElectionVote>? ElectionVotes = null,
    [property: JsonPropertyName("markdown_to_html")] IReadOnlyDictionary<string, string>? MarkdownToHtml = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>? Bookmarks = null,
    [property: JsonPropertyName("post_link_embeds")] IReadOnlyDictionary<string, UrlEmbed>? PostLinkEmbeds = null);

public sealed record Notification(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("entity_type")] string? EntityType,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("body")] string? Body,
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
    [property: JsonPropertyName("actor_label")] string? ActorLabel = null,
    [property: JsonPropertyName("moderation_report_id")] string? ModerationReportId = null,
    [property: JsonPropertyName("pushed_at")] DateTimeOffset? PushedAt = null,
    [property: JsonPropertyName("review_dispute_id")] string? ReviewDisputeId = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("user_warning_id")] string? UserWarningId = null,
    [property: JsonPropertyName("copyright_notice_id")] string? CopyrightNoticeId = null);

public sealed record NotificationsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("notifications")] IReadOnlyDictionary<string, Notification> Notifications,
    [property: JsonPropertyName("communities")] IReadOnlyDictionary<string, NotificationCommunity>? Communities = null);

public sealed record NotificationTargetEntity(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("id")] string Id);

public sealed record NotificationCommunity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name);

public sealed record PostDetailAuthorAside(
    [property: JsonPropertyName("about_html")] string AboutHtml,
    [property: JsonPropertyName("is_following")] bool IsFollowing,
    [property: JsonPropertyName("profile_links")] IReadOnlyList<ProfileLink> ProfileLinks);

public sealed record PostDetailResponse(
    [property: JsonPropertyName("author_aside")] PostDetailAuthorAside? AuthorAside,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>? Bookmarks,
    [property: JsonPropertyName("election_vote")] ElectionVote? ElectionVote,
    [property: JsonPropertyName("html")] string? Html,
    [property: JsonPropertyName("post")] Post Post,
    [property: JsonPropertyName("post_election")] PostElection? PostElection,
    [property: JsonPropertyName("post_metrics")] PostMetrics? PostMetrics,
    [property: JsonPropertyName("link_embed")] UrlEmbed? LinkEmbed = null);

public sealed record FetchNotificationsRequest(
    string? After = null,
    int Limit = 20);

public sealed record NotificationRedirectTargetResponse(
    [property: JsonPropertyName("target_url")] string TargetUrl);

public sealed record UserFollowingResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<User> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("muted")] IReadOnlyDictionary<string, bool>? Muted = null) : IPageOfUsers;

public sealed record MyIdentityResponse([property: JsonPropertyName("identity")] User Identity);

public sealed record MyProfile(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("markdown")] string? Markdown);

public sealed record MyProfileResponse([property: JsonPropertyName("profile")] MyProfile Profile);

#pragma warning restore CA1054, CA1056, CA1720
