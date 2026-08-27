using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Profiles;

public sealed class ApiProfileCollectionsService : IProfileCollectionsService
{
  private readonly VouchaApiClient client;

  public ApiProfileCollectionsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public async Task<ProfileCollectionPage<Topic>> FetchTopicsAsync(
      string userId,
      string? after,
      CancellationToken cancellationToken = default)
  {
    var response = await client.FetchUserTopicsCollectionAsync(
        userId, "following", after, cancellationToken: cancellationToken).ConfigureAwait(false);
    return new(response.Results, response.PageInfo);
  }

  public async Task<ProfileCollectionPage<User>> FetchFollowingAsync(
      string userId,
      string? after,
      CancellationToken cancellationToken = default)
  {
    var response = await client.FetchUserFollowingAsync(
        new(userId, After: after, Limit: 25), cancellationToken).ConfigureAwait(false);
    return new(response.Results, response.PageInfo);
  }

  public async Task<ProfileCollectionPage<User>> FetchFollowersAsync(
      string userId,
      string? after,
      CancellationToken cancellationToken = default)
  {
    var response = await client.FetchUserFollowersAsync(
        new(userId, After: after, Limit: 25), cancellationToken).ConfigureAwait(false);
    return new(response.Results, response.PageInfo);
  }

  public async Task<ProfileCollectionPage<RssFeedSource>> FetchSourcesAsync(
      string userId,
      string? feedType,
      string? after,
      CancellationToken cancellationToken = default)
  {
    var response = await client.FetchUserRssFeedsAsync(
        new(userId, FeedType: feedType, After: after), cancellationToken).ConfigureAwait(false);
    return new(response.Results, response.PageInfo);
  }

  public async Task<ProfileCollectionPage<Community>> FetchCommunitiesAsync(
      string userId,
      string? after,
      CancellationToken cancellationToken = default)
  {
    var response = await client.FetchUserCommunitiesCollectionAsync(
        userId, "member", after, cancellationToken: cancellationToken).ConfigureAwait(false);
    return new(response.Results, response.PageInfo);
  }
}
