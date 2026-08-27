namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest Lists(string? after = null, int limit = 25) =>
      Get("/api/v1/lists", Query(("limit", limit), ("after", after)));

  public static ApiRequest CreateList(ListMutationBody body) =>
      new(HttpMethod.Post, "/api/v1/lists") { Body = body };

  public static ApiRequest List(string listId) =>
      Get($"/api/v1/lists/{Path(listId)}");

  public static ApiRequest UpdateList(string listId, ListMutationBody body) =>
      new(HttpMethod.Patch, $"/api/v1/lists/{Path(listId)}") { Body = body };

  public static ApiRequest DeleteList(string listId) =>
      new(HttpMethod.Delete, $"/api/v1/lists/{Path(listId)}");

  public static ApiRequest ListItems(
      string listId,
      string? mediaType = null,
      bool? read = null,
      string? after = null,
      int limit = 25) =>
      Get(
          $"/api/v1/lists/{Path(listId)}/items",
          Query(("limit", limit), ("media_type", mediaType), ("read", Bool(read)), ("after", after)));

  public static ApiRequest AddListRssFeedItem(string listId, AddListRssFeedItemBody body) =>
      new(HttpMethod.Post, $"/api/v1/lists/{Path(listId)}/items/rss-feed-items") { Body = body };

  public static ApiRequest RemoveListRssFeedItem(string listId, string rssFeedItemId) =>
      new(HttpMethod.Delete, $"/api/v1/lists/{Path(listId)}/items/rss-feed-items/{Path(rssFeedItemId)}");

  public static ApiRequest AddListPost(string listId, AddListPostBody body) =>
      new(HttpMethod.Post, $"/api/v1/lists/{Path(listId)}/items/posts") { Body = body };

  public static ApiRequest RemoveListPost(string listId, string postId) =>
      new(HttpMethod.Delete, $"/api/v1/lists/{Path(listId)}/items/posts/{Path(postId)}");

  public static ApiRequest ListsContaining(string itemType, string entityId) =>
      Get("/api/v1/lists/contains", Query(("item_type", itemType), ("entity_id", entityId)));

  public static ApiRequest ImportCommunityList(string listId, ImportCommunityListBody body) =>
      new(HttpMethod.Post, $"/api/v1/lists/{Path(listId)}/import") { Body = body };
}
