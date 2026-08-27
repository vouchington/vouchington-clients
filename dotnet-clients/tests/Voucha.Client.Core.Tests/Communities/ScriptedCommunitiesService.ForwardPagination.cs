using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;

namespace Voucha.Client.Core.Tests.Communities;

internal sealed partial class ScriptedCommunitiesService
{
  public Queue<RssFeedItemsFeedResponse> NewsResponses { get; } = [];
  public Queue<ModlogResponse> ModlogResponses { get; } = [];
  public List<(CommunityDetailSurfaceSection Section, string? After, int? Limit)> ForwardPageRequests { get; } = [];
  public Task<CommunityApplicationsResponse>? DelayedApplicationsContinuation { get; set; }
  public TaskCompletionSource ApplicationsContinuationStarted { get; } =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  public Task<CommunityMembersResponse> FetchMembersPageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      Next(CommunityDetailSurfaceSection.Members, after, limit, MembersResponses,
          () => FetchMembersAsync(idOrSlug, cancellationToken));

  public Task<CommunityPostsResponse> FetchPostsPageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      Next(CommunityDetailSurfaceSection.Posts, after, limit, PostsResponses,
          () => FetchPostsAsync(idOrSlug, cancellationToken));

  public Task<RssFeedItemsFeedResponse> FetchNewsPageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      Next(CommunityDetailSurfaceSection.News, after, limit, NewsResponses,
          () => FetchNewsAsync(idOrSlug, cancellationToken));

  public Task<CommunityApplicationsResponse> FetchApplicationsPageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default)
  {
    if (after is not null && DelayedApplicationsContinuation is { } continuation)
    {
      ForwardPageRequests.Add((CommunityDetailSurfaceSection.Applications, after, limit));
      ApplicationsContinuationStarted.TrySetResult();
      return continuation;
    }

    return Next(CommunityDetailSurfaceSection.Applications, after, limit, ApplicationsResponses,
        () => FetchApplicationsAsync(idOrSlug, cancellationToken));
  }

  public Task<CommunityInvitesResponse> FetchInvitesPageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      Next(CommunityDetailSurfaceSection.Invites, after, limit, InvitesResponses,
          () => FetchInvitesAsync(idOrSlug, cancellationToken));

  public Task<CommunityBansResponse> FetchBansPageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      Next(CommunityDetailSurfaceSection.Bans, after, limit, BansResponses,
          () => FetchBansAsync(idOrSlug, cancellationToken));

  public Task<CommunityRestrictionsResponse> FetchRestrictionsPageAsync(
      string idOrSlug, string? after, CancellationToken cancellationToken = default) =>
      Next(CommunityDetailSurfaceSection.Restrictions, after, null, RestrictionsResponses,
          () => FetchRestrictionsAsync(idOrSlug, cancellationToken));

  public Task<ModlogResponse> FetchModlogPageAsync(
      string idOrSlug, string? after, CancellationToken cancellationToken = default) =>
      Next(CommunityDetailSurfaceSection.Modlog, after, null, ModlogResponses,
          () => FetchModlogAsync(idOrSlug, cancellationToken));

  public Task<CommunityModerationQueueResponse> FetchModerationQueuePageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      Next(CommunityDetailSurfaceSection.Moderation, after, limit, ModerationQueueResponses,
          () => FetchModerationQueueAsync(idOrSlug, cancellationToken));

  private Task<T> Next<T>(
      CommunityDetailSurfaceSection section,
      string? after,
      int? limit,
      Queue<T> responses,
      Func<Task<T>> fallback)
  {
    ForwardPageRequests.Add((section, after, limit));
    return responses.Count > 0 ? Task.FromResult(responses.Dequeue()) : fallback();
  }
}
