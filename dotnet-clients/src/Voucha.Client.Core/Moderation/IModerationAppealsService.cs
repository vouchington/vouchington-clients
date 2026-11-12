using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public interface IModerationAppealsService
{
  Task<StaffModerationAppealListResponse> FetchAppealsAsync(
      ModerationAppealStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<ModerationAppealResponse> FetchAppealAsync(string id, CancellationToken cancellationToken = default);

  Task<ModerationAppealResolutionDraftRunResponse> FetchAIRerunAsync(
      string appealId,
      string runId,
      CancellationToken cancellationToken = default);

  Task<ModerationAppealResponse> UpdatePublicResponseAsync(string id, string publicResponse, CancellationToken cancellationToken = default);

  Task<ModerationAppealResponse> ApproveAsync(string id, CancellationToken cancellationToken = default);

  Task<ModerationAppealResponse> DeliverAsync(string id, CancellationToken cancellationToken = default);

  Task<ModerationAppealResponse> ResolveAsync(string id, ModerationAppealAction action, CancellationToken cancellationToken = default);

  Task<ModerationAppealQueueResponse> RerunResolutionDraftAsync(string id, CancellationToken cancellationToken = default);
}
