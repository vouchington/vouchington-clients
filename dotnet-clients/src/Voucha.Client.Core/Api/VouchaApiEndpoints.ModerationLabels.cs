using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record TopicClaimRejectionBody(
    [property: JsonPropertyName("rejection_reason")] string RejectionReason);
public sealed record TopicClaimRevocationBody(
    [property: JsonPropertyName("revocation_reason")] string RevocationReason);
public sealed record CreateUserModerationNoteBody(
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("community_id")] string? CommunityId);

public static partial class VouchaApiEndpoints
{
  public static ApiRequest PendingTopicClaims(string? after = null, int? limit = null) =>
      Get("/api/v1/admin/topic-claims", Query(("after", after), ("limit", limit)));
  public static ApiRequest VerifyTopicClaim(string id) =>
      new(HttpMethod.Post, $"/api/v1/admin/topic-claims/{Path(id)}/verification");
  public static ApiRequest RejectTopicClaim(string id, string reason) =>
      new(HttpMethod.Post, $"/api/v1/admin/topic-claims/{Path(id)}/rejection") { Body = new TopicClaimRejectionBody(reason) };
  public static ApiRequest RevokeTopicClaim(string id, string reason) =>
      new(HttpMethod.Post, $"/api/v1/admin/topic-claims/{Path(id)}/revocation") { Body = new TopicClaimRevocationBody(reason) };
  public static ApiRequest UserModerationNotes(string id, string? after = null, int limit = 20) =>
      Get($"/api/v1/users/{Path(id)}/mod-notes", Query(("after", after), ("limit", limit)));
  public static ApiRequest CreateUserModerationNote(string id, CreateUserModerationNoteBody body) =>
      new(HttpMethod.Post, $"/api/v1/users/{Path(id)}/mod-notes") { Body = body };
}
