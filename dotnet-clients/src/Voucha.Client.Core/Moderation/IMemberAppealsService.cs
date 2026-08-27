using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public interface IMemberAppealsService
{
  Task<ModerationAppealListResponse> FetchAppealsAsync(
      ModerationAppealStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<MemberWarningNoticesResponse> FetchWarningsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<PersonalCommunityBansResponse> FetchBansAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<PersonalRemovedPostsResponse> FetchRemovedPostsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<MyIdentityResponse> FetchIdentityAsync(
      CancellationToken cancellationToken = default);

  Task<ModerationAppealSubmissionResponse> SubmitAsync(
      ModerationAppealSubmissionRequest request,
      CancellationToken cancellationToken = default);
}
