using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record TopicClaim(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("topic_id")] string TopicId,
    [property: JsonPropertyName("claimant_user_id")] string ClaimantUserId,
    [property: JsonPropertyName("verification_method")] string? VerificationMethod,
    [property: JsonPropertyName("claimed_role")] string ClaimedRole,
    [property: JsonPropertyName("evidence")] string Evidence,
    [property: JsonPropertyName("submitted_at")] string? SubmittedAt,
    [property: JsonPropertyName("verified_at")] string? VerifiedAt,
    [property: JsonPropertyName("verified_by_id")] string? VerifiedById,
    [property: JsonPropertyName("rejected_at")] string? RejectedAt,
    [property: JsonPropertyName("rejected_by_id")] string? RejectedById,
    [property: JsonPropertyName("rejection_reason")] string? RejectionReason,
    [property: JsonPropertyName("revoked_at")] string? RevokedAt,
    [property: JsonPropertyName("revoked_by_id")] string? RevokedById,
    [property: JsonPropertyName("revocation_reason")] string? RevocationReason,
    [property: JsonPropertyName("verification_hostname_id")] string? VerificationHostnameId,
    [property: JsonPropertyName("verification_token_issued_at")] string? VerificationTokenIssuedAt,
    [property: JsonPropertyName("domain_verified_at")] string? DomainVerifiedAt,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("updated_at")] string UpdatedAt,
    [property: JsonPropertyName("state")] string State);

public sealed record TopicClaimPage(
    [property: JsonPropertyName("results")] IReadOnlyList<TopicClaim> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
