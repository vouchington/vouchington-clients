using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record EntityRelationRef(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("id")] string Id);

public sealed record EntityRelation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("subject_id")] string? SubjectId = null,
    [property: JsonPropertyName("object_id")] string? ObjectId = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("created_by_id")] string? CreatedById = null,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById = null,
    [property: JsonPropertyName("order_index")] int? OrderIndex = null,
    [property: JsonPropertyName("votes_count_up")] int? VotesCountUp = null,
    [property: JsonPropertyName("votes_count_down")] int? VotesCountDown = null,
    [property: JsonPropertyName("votes_score_net")] double? VotesScoreNet = null,
    [property: JsonPropertyName("votes_score_sort")] double? VotesScoreSort = null,
    [property: JsonPropertyName("object_data")] object? ObjectData = null);

public sealed record EntityRelationElection(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("votes_score_net")] double VotesScoreNet,
    [property: JsonPropertyName("votes_count_up")] int VotesCountUp,
    [property: JsonPropertyName("votes_count_down")] int VotesCountDown);

public sealed record EntityRelationVote(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("choice")] ElectionVoteChoice Choice,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record EntityRelationsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityRelationRef> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("entity_relations")] IReadOnlyDictionary<string, EntityRelation> EntityRelations,
    [property: JsonPropertyName("entity_relation_elections")] IReadOnlyDictionary<string, EntityRelationElection>? EntityRelationElections = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, EntityRelationVote>? ElectionVotes = null);

public sealed record EntityRelationResponse([property: JsonPropertyName("relation")] EntityRelation Relation);

public sealed record ImageUploadUrl(
    [property: JsonPropertyName("image_id")] string ImageId,
    [property: JsonPropertyName("upload_url")] string UploadUrl,
    [property: JsonPropertyName("content_type")] string ContentType,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);

public sealed record ImageUploadUrlResponse([property: JsonPropertyName("upload")] ImageUploadUrl Upload);

public sealed record ImageUpload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("upload_status")] string UploadStatus,
    [property: JsonPropertyName("upload_error")] string? UploadError = null);

public sealed record CompleteImageUploadResponse([property: JsonPropertyName("image")] ImageUpload Image);

public sealed record ImageUploadState(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("upload_status")] string UploadStatus,
    [property: JsonPropertyName("upload_error")] string? UploadError,
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("blocked")] bool Blocked);

public sealed record ImageUploadStateResponse([property: JsonPropertyName("upload_state")] ImageUploadState UploadState);

#pragma warning restore CA1054, CA1056, CA1720
