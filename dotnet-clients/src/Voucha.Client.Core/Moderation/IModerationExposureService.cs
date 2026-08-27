using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public interface IModerationExposureService
{
  Task<ModerationExposureResponse> FetchExposureAsync(
      CancellationToken cancellationToken = default);

  Task<ModerationExposureResponse> RecordReviewQueueRevealAsync(
      string postId,
      CancellationToken cancellationToken = default);
}
