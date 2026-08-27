namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest EntityBookmarks(string entityType, string entityId) =>
      Get($"/api/v1/bookmarks/{Path(entityType)}/{Path(entityId)}");

  public static ApiRequest UserPosts(
      string userId,
      string listType,
      string? mediaType = null,
      string? after = null,
      int limit = 25) =>
      Get(
          $"/api/v1/users/{Path(userId)}/posts/{Path(listType)}",
          Query(("limit", limit), ("media_type", mediaType), ("after", after)));

  public static ApiRequest UserTopics(
      string userId,
      string listType,
      string? after = null,
      int limit = 25) =>
      Get(
          $"/api/v1/users/{Path(userId)}/topics/{Path(listType)}",
          Query(("limit", limit), ("after", after)));

  public static ApiRequest UserUsers(
      string userId,
      string listType,
      string? after = null,
      int limit = 25) =>
      Get(
          $"/api/v1/users/{Path(userId)}/users/{Path(listType)}",
          Query(("limit", limit), ("after", after)));

  public static ApiRequest UserCommunities(
      string userId,
      string listType,
      string? after = null,
      int limit = 25) =>
      Get(
          $"/api/v1/users/{Path(userId)}/communities/{Path(listType)}",
          Query(("limit", limit), ("after", after)));

  public static ApiRequest UserRssFeedItems(
      string userId,
      string listType,
      string? mediaType = null,
      string? after = null,
      int limit = 25) =>
      Get(
          $"/api/v1/users/{Path(userId)}/rss-feed-items/{Path(listType)}",
          Query(("limit", limit), ("media_type", mediaType), ("after", after)));
}
