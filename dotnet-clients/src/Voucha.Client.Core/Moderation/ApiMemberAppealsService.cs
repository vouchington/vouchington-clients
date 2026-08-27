using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed class ApiMemberAppealsService(VouchaApiClient client) : IMemberAppealsService
{
  public Task<ModerationAppealListResponse> FetchAppealsAsync(
      ModerationAppealStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchAppealsAsync(status, limit, after, mine: true, cancellationToken);

  public Task<MemberWarningNoticesResponse> FetchWarningsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchPersonalWarningsAsync(after, limit, cancellationToken);

  public Task<PersonalCommunityBansResponse> FetchBansAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchPersonalCommunityBansAsync(after, limit, cancellationToken);

  public Task<PersonalRemovedPostsResponse> FetchRemovedPostsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchPersonalRemovedPostsAsync(after, limit, cancellationToken);

  public Task<MyIdentityResponse> FetchIdentityAsync(
      CancellationToken cancellationToken = default) =>
      client.FetchMyIdentityAsync(cancellationToken);

  public Task<ModerationAppealSubmissionResponse> SubmitAsync(
      ModerationAppealSubmissionRequest request,
      CancellationToken cancellationToken = default) =>
      client.SubmitAppealAsync(request, cancellationToken);
}
