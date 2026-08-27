namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<BookmarkCollectionResponse<Post>> FetchUserPostsCollectionAsync(
      string userId,
      string listType,
      string? mediaType = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<Post>>(
          VouchaApiEndpoints.UserPosts(userId, listType, mediaType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<BookmarkCollectionReference>> FetchUserPostBookmarkReferencesAsync(
      string userId,
      string listType,
      string? mediaType = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<BookmarkCollectionReference>>(
          VouchaApiEndpoints.UserPosts(userId, listType, mediaType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<Topic>> FetchUserTopicsCollectionAsync(
      string userId,
      string listType,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<Topic>>(
          VouchaApiEndpoints.UserTopics(userId, listType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<BookmarkCollectionReference>> FetchUserTopicBookmarkReferencesAsync(
      string userId,
      string listType,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<BookmarkCollectionReference>>(
          VouchaApiEndpoints.UserTopics(userId, listType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<User>> FetchUserUsersCollectionAsync(
      string userId,
      string listType,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<User>>(
          VouchaApiEndpoints.UserUsers(userId, listType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<BookmarkCollectionReference>> FetchUserUserBookmarkReferencesAsync(
      string userId,
      string listType,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<BookmarkCollectionReference>>(
          VouchaApiEndpoints.UserUsers(userId, listType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<Community>> FetchUserCommunitiesCollectionAsync(
      string userId,
      string listType,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<Community>>(
          VouchaApiEndpoints.UserCommunities(userId, listType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<BookmarkCollectionReference>> FetchUserCommunityBookmarkReferencesAsync(
      string userId,
      string listType,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<BookmarkCollectionReference>>(
          VouchaApiEndpoints.UserCommunities(userId, listType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<RssFeedItem>> FetchUserRssFeedItemsCollectionAsync(
      string userId,
      string listType,
      string? mediaType = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<RssFeedItem>>(
          VouchaApiEndpoints.UserRssFeedItems(userId, listType, mediaType, after, limit),
          cancellationToken);

  public Task<BookmarkCollectionResponse<BookmarkCollectionReference>> FetchUserRssFeedItemBookmarkReferencesAsync(
      string userId,
      string listType,
      string? mediaType = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<BookmarkCollectionResponse<BookmarkCollectionReference>>(
          VouchaApiEndpoints.UserRssFeedItems(userId, listType, mediaType, after, limit),
          cancellationToken);
}
