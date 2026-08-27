using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record MemberWarningNotice(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string? CaseId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("community_id")] string? CommunityId,
    [property: JsonPropertyName("community_slug")] string? CommunitySlug,
    [property: JsonPropertyName("public_message")] string? PublicMessage,
    [property: JsonPropertyName("revoked_at")] DateTimeOffset? RevokedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record MemberWarningNoticesResponse(
    [property: JsonPropertyName("warnings")] IReadOnlyList<MemberWarningNotice> Warnings,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record PersonalCommunityBan(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("community_slug")] string? CommunitySlug,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("lifted_at")] DateTimeOffset? LiftedAt);

public sealed record PersonalCommunityBansResponse(
    [property: JsonPropertyName("bans")] IReadOnlyList<PersonalCommunityBan> Bans,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record PersonalRemovedPost(
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("post_title")] string? PostTitle,
    [property: JsonPropertyName("community_id")] string? CommunityId,
    [property: JsonPropertyName("community_slug")] string? CommunitySlug,
    [property: JsonPropertyName("unpublished_at")] DateTimeOffset UnpublishedAt,
    [property: JsonPropertyName("post_removal_kind")] string? PostRemovalKind,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record PersonalRemovedPostsResponse(
    [property: JsonPropertyName("removed_posts")] IReadOnlyList<PersonalRemovedPost> RemovedPosts,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
