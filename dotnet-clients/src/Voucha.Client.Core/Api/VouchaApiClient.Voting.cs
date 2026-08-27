namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task VotePostAsync(string postId, ElectionVoteChoice choice, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.VotePost(postId, choice), cancellationToken);

  public Task ClearPostVoteAsync(string postId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClearPostVote(postId), cancellationToken);

  public Task VoteTopicAsync(string topicId, ElectionVoteChoice choice, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.VoteTopic(topicId, choice), cancellationToken);

  public Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClearTopicVote(topicId), cancellationToken);

  public Task VoteUserTrustAsync(string userId, ElectionVoteChoice choice, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.VoteUserTrust(userId, choice), cancellationToken);

  public Task ClearUserTrustVoteAsync(string userId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClearUserTrustVote(userId), cancellationToken);

  public Task<UserTrustContext> FetchUserTrustContextAsync(string userId, CancellationToken cancellationToken = default) =>
      SendAsync<UserTrustContext>(VouchaApiEndpoints.UserTrustContext(userId), cancellationToken);

  public Task VoteRssFeedItemAsync(
      string rssFeedItemId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.VoteRssFeedItem(rssFeedItemId, choice), cancellationToken);

  public Task ClearRssFeedItemVoteAsync(string rssFeedItemId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClearRssFeedItemVote(rssFeedItemId), cancellationToken);
}
