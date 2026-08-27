using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Profiles;

public sealed record ProfileCollectionPage<T>(IReadOnlyList<T> Results, PageInfo PageInfo);

public interface IProfileCollectionsService
{
  Task<ProfileCollectionPage<Topic>> FetchTopicsAsync(string userId, string? after, CancellationToken cancellationToken = default);

  Task<ProfileCollectionPage<User>> FetchFollowingAsync(string userId, string? after, CancellationToken cancellationToken = default);

  Task<ProfileCollectionPage<User>> FetchFollowersAsync(string userId, string? after, CancellationToken cancellationToken = default);

  Task<ProfileCollectionPage<RssFeedSource>> FetchSourcesAsync(
      string userId,
      string? feedType,
      string? after,
      CancellationToken cancellationToken = default);

  Task<ProfileCollectionPage<Community>> FetchCommunitiesAsync(string userId, string? after, CancellationToken cancellationToken = default);
}
