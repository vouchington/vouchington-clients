using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

// Community list-item request bodies intentionally use URL-shaped field names that
// mirror the backend contract.
#pragma warning disable CA1054, CA1056, CA1720

public sealed record CreateCommunityRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("visibility")] string? Visibility = null,
    [property: JsonPropertyName("list_type")] string? ListType = null,
    [property: JsonPropertyName("member_roster_visibility")] string? MemberRosterVisibility = null,
    [property: JsonPropertyName("post_approval_required_at")] bool? PostApprovalRequiredAt = null,
    [property: JsonPropertyName("allow_review_posts")] bool? AllowReviewPosts = null,
    [property: JsonPropertyName("allow_data_point_posts")] bool? AllowDataPointPosts = null,
    [property: JsonPropertyName("member_invites_allowed_at")] bool? MemberInvitesAllowedAt = null,
    [property: JsonPropertyName("cf_turnstile_response")] string? TurnstileToken = null);

public sealed record UpdateCommunityRequest(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("markdown")] JsonNullableString? Markdown = null,
    [property: JsonPropertyName("visibility")] string? Visibility = null,
    [property: JsonPropertyName("list_type")] JsonNullableString? ListType = null,
    [property: JsonPropertyName("member_roster_visibility")] string? MemberRosterVisibility = null,
    [property: JsonPropertyName("post_approval_required_at")] bool? PostApprovalRequiredAt = null,
    [property: JsonPropertyName("member_invites_allowed_at")] bool? MemberInvitesAllowedAt = null,
    [property: JsonPropertyName("archive")] bool? Archive = null);

public sealed record ApplyToCommunityRequest(
    [property: JsonPropertyName("answers")] IReadOnlyDictionary<string, object> Answers,
    [property: JsonPropertyName("message")] string? Message = null);

public sealed record UpdateCommunityMemberRequest(
    [property: JsonPropertyName("role")] string Role);

public sealed record CommunityListItemRequest(
    [property: JsonPropertyName("topic_id")] string? TopicId = null,
    [property: JsonPropertyName("rss_feed_id")] string? RssFeedId = null,
    [property: JsonPropertyName("post_id")] string? PostId = null,
    [property: JsonPropertyName("url_hostname_id")] string? UrlHostnameId = null,
    [property: JsonPropertyName("url_id")] string? UrlId = null);

public sealed record UpdateCommunityPostTypeSettingsRequest(
    [property: JsonPropertyName("allow_review_posts")] bool? AllowReviewPosts = null,
    [property: JsonPropertyName("allow_data_point_posts")] bool? AllowDataPointPosts = null);

public sealed record OpenCommunityModmailRequest(
    [property: JsonPropertyName("subject_user_id")] string? SubjectUserId = null);

public sealed record UpdateCommunityModmailThreadRequest(
    [property: JsonPropertyName("assigned_mod_id")] string? AssignedModId = null,
    [property: JsonPropertyName("resolved")] bool? Resolved = null);

public sealed record CreateCommunitySavedReplyRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body);

public sealed record IssueCommunityWarningRequest(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("publicMessage")] string? PublicMessage = null,
    [property: JsonPropertyName("reportId")] string? ReportId = null,
    [property: JsonPropertyName("resolveReport")] bool? ResolveReport = null);

public sealed record CommunityPlatformModeration(
    [property: JsonPropertyName("status")] AdminReviewQueueClearanceStatus Status);

public sealed record CommunityModerationResultsResponse(
    [property: JsonPropertyName("community_agent_moderations")] IReadOnlyList<JsonElement> CommunityAgentModerations,
    [property: JsonPropertyName("platform_moderation")] CommunityPlatformModeration PlatformModeration);
#pragma warning restore CA1054, CA1056, CA1720
