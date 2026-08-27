namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<PostsFeedResponse> SearchPostsAsync(
      string query,
      int limit = 10,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostsFeedResponse>(
          VouchaApiEndpoints.SearchPosts(query, limit),
          cancellationToken);

  public Task VoteEntityRelationAsync(
      string relationId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.VoteEntityRelation(relationId, choice), cancellationToken);

  public Task ClearEntityRelationVoteAsync(string relationId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClearEntityRelationVote(relationId), cancellationToken);

  public Task<PublisherTypeTopicsResponse> FetchPublisherTypesAsync(
      CancellationToken cancellationToken = default) =>
      SendAsync<PublisherTypeTopicsResponse>(
          VouchaApiEndpoints.PublisherTypes(),
          cancellationToken);

  public Task<UserTagTopicsResponse> FetchUserTagsAsync(
      CancellationToken cancellationToken = default) =>
      SendAsync<UserTagTopicsResponse>(VouchaApiEndpoints.UserTags(), cancellationToken);
}
