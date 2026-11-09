using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record TopicClaimMutationResponse(
    [property: JsonPropertyName("claim")] TopicClaim Claim);

public sealed record UserModerationNote(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("target_user_id")] string TargetUserId,
    [property: JsonPropertyName("author_user_id")] string? AuthorUserId,
    [property: JsonPropertyName("community_id")] string? CommunityId,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt,
    [property: JsonPropertyName("author_username")] string? AuthorUsername,
    [property: JsonPropertyName("author_display_name")] string? AuthorDisplayName,
    [property: JsonPropertyName("community_name")] string? CommunityName);

public sealed record UserModerationNotesResponse(
    [property: JsonPropertyName("notes")] IReadOnlyList<UserModerationNote> Notes,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record UserModerationNoteResponse(
    [property: JsonPropertyName("note")] UserModerationNote Note);

public sealed record PendingTopicClaimsResponse(
    [property: JsonPropertyName("claims")] IReadOnlyList<TopicClaim> Claims);
