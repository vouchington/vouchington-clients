namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<PendingTopicClaimsResponse> FetchPendingTopicClaimsAsync(string? after = null, int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<PendingTopicClaimsResponse>(VouchaApiEndpoints.PendingTopicClaims(after, limit), cancellationToken);
  public Task<TopicClaimMutationResponse> VerifyTopicClaimAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync<TopicClaimMutationResponse>(VouchaApiEndpoints.VerifyTopicClaim(id), cancellationToken);
  public Task<TopicClaimMutationResponse> RejectTopicClaimAsync(string id, string reason, CancellationToken cancellationToken = default) =>
      SendAsync<TopicClaimMutationResponse>(VouchaApiEndpoints.RejectTopicClaim(id, reason), cancellationToken);
  public Task<TopicClaimMutationResponse> RevokeTopicClaimAsync(string id, string reason, CancellationToken cancellationToken = default) =>
      SendAsync<TopicClaimMutationResponse>(VouchaApiEndpoints.RevokeTopicClaim(id, reason), cancellationToken);
  public Task<UserModerationNotesResponse> FetchUserModerationNotesAsync(string id, string? after = null, int limit = 20,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserModerationNotesResponse>(VouchaApiEndpoints.UserModerationNotes(id, after, limit), cancellationToken);
  public Task<UserModerationNoteResponse> CreateUserModerationNoteAsync(string id, CreateUserModerationNoteBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserModerationNoteResponse>(VouchaApiEndpoints.CreateUserModerationNote(id, body), cancellationToken);
}
