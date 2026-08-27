namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<FollowerDistributionAcceptedResponse> SharePostWithFollowersAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<FollowerDistributionAcceptedResponse>(
          VouchaApiEndpoints.SharePostWithFollowers(postIdOrSlug), cancellationToken);

  public Task<FollowerDistributionAcceptedResponse> SendPostToFollowersAsync(
      string postIdOrSlug,
      FollowerDistributionBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<FollowerDistributionAcceptedResponse>(
          VouchaApiEndpoints.SendPostToFollowers(postIdOrSlug, body), cancellationToken);

  public Task<FollowerDistributionAcceptedResponse> ShareRssFeedItemWithFollowersAsync(
      string rssFeedItemId,
      CancellationToken cancellationToken = default) =>
      SendAsync<FollowerDistributionAcceptedResponse>(
          VouchaApiEndpoints.ShareRssFeedItemWithFollowers(rssFeedItemId), cancellationToken);

  public Task<FollowerDistributionAcceptedResponse> SendRssFeedItemToFollowersAsync(
      string rssFeedItemId,
      FollowerDistributionBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<FollowerDistributionAcceptedResponse>(
          VouchaApiEndpoints.SendRssFeedItemToFollowers(rssFeedItemId, body), cancellationToken);
}
