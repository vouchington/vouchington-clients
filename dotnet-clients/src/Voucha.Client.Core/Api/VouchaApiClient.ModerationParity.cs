namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<ModerationDisputeResponse> FetchDisputeAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationDisputeResponse>(
          VouchaApiEndpoints.Dispute(id),
          cancellationToken);

  public Task<ModerationDisputeResponse> UpdateDisputeAsync(
      string id,
      string? publicResponse,
      string? internalNotes,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationDisputeResponse>(
          VouchaApiEndpoints.UpdateDispute(id, publicResponse, internalNotes),
          cancellationToken);

  public Task<ModerationDisputeResponse> ApproveDisputeAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationDisputeResponse>(
          VouchaApiEndpoints.DisputeApproval(id),
          cancellationToken);

  public Task<ModerationDisputeResponse> DeliverDisputeAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationDisputeResponse>(
          VouchaApiEndpoints.DisputeDelivery(id),
          cancellationToken);

  public Task<ModerationDisputeResponse> ResolveDisputeAsync(
      string id,
      ModerationDisputeResolutionAction action,
      string? bodyText = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationDisputeResponse>(
          VouchaApiEndpoints.DisputeResolution(id, action, bodyText),
          cancellationToken);

  public Task<ModerationDisputeQueueResponse> RerunDisputeResolutionDraftAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationDisputeQueueResponse>(
          VouchaApiEndpoints.DisputeResolutionDrafts(id),
          cancellationToken);

  public Task<ModerationExposureResponse> FetchModerationExposureAsync(
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationExposureResponse>(
          VouchaApiEndpoints.ModerationExposure(),
          cancellationToken);

  public Task<ModerationExposureResponse> RecordModerationRevealAsync(
      string? postId,
      string? reportId,
      ModerationRevealSurface surface,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationExposureResponse>(
          VouchaApiEndpoints.RecordModerationReveal(postId, reportId, surface),
          cancellationToken);
}
