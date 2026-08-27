using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public interface IModerationDisputesService
{
  Task<ModerationDisputeListResponse> FetchDisputesAsync(
      ModerationDisputeStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<ModerationDisputeResponse> FetchDisputeAsync(
      string id,
      CancellationToken cancellationToken = default);

  Task<ModerationDisputeResponse> UpdatePublicResponseAsync(
      string id,
      string publicResponse,
      CancellationToken cancellationToken = default);

  Task<ModerationDisputeResponse> ApproveAsync(
      string id,
      CancellationToken cancellationToken = default);

  Task<ModerationDisputeResponse> DeliverAsync(
      string id,
      CancellationToken cancellationToken = default);

  Task<ModerationDisputeResponse> ResolveAsync(
      string id,
      ModerationDisputeResolutionAction action,
      string? annotation,
      CancellationToken cancellationToken = default);

  Task<ModerationDisputeQueueResponse> RerunResolutionDraftAsync(
      string id,
      CancellationToken cancellationToken = default);
}
