using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record UserModerationContextResponse(
    [property: JsonPropertyName("context")] UserModerationContext Context,
    [property: JsonPropertyName("notes")] IReadOnlyList<UserModNote> Notes,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record UserModerationContext(
    [property: JsonPropertyName("account_age_ms")] long AccountAgeMs,
    [property: JsonPropertyName("trust_tier")] int? TrustTier,
    [property: JsonPropertyName("active_suspension")] UserModerationSuspension? ActiveSuspension,
    [property: JsonPropertyName("content_removal_count")] int? ContentRemovalCount,
    [property: JsonPropertyName("content_removal_count_capped")] bool? ContentRemovalCountCapped,
    [property: JsonPropertyName("community_removal_count")] int? CommunityRemovalCount);

public sealed record UserModerationSuspension(
    [property: JsonPropertyName("suspended_at")] string SuspendedAt,
    [property: JsonPropertyName("suspended_reason")] string? SuspendedReason);

public sealed record UserModNote(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("target_user_id")] string TargetUserId,
    [property: JsonPropertyName("author_user_id")] string AuthorUserId,
    [property: JsonPropertyName("author_username")] string? AuthorUsername,
    [property: JsonPropertyName("author_display_name")] string? AuthorDisplayName,
    [property: JsonPropertyName("community_id")] string? CommunityId,
    [property: JsonPropertyName("community_name")] string? CommunityName,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("deleted_at")] string? DeletedAt);
