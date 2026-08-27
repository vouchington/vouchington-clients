using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchListsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.lists.default");

    var response = await client.FetchListsAsync(cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/lists?limit=25");
    Assert.Equal("list-1", response.Results[0].Id);
    Assert.Equal("Reading Queue", response.Lists["list-1"].Name);
    Assert.Equal("private", response.Lists["list-1"].Visibility);
  }

  [Fact]
  public async Task FetchListItemsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.list-items.default");

    var response = await client.FetchListItemsAsync(
        new FetchListItemsRequest("list-1"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/lists/list-1/items?limit=25");
    Assert.Equal("list-item-1", response.Results[0].Id);
    Assert.Equal("rss_feed_item", response.ListItems["list-item-1"].ItemType);
    Assert.Equal("article", response.ListItems["list-item-1"].MediaType);
  }

  [Fact]
  public async Task FetchListsContainingAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.lists-containing.default");

    var response = await client.FetchListsContainingAsync(
        "rss_feed_item",
        "item-1",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/lists/contains?entity_id=item-1&item_type=rss_feed_item");
    Assert.Equal("list-1", response.ListIds[0]);
  }

  [Fact]
  public async Task ImportCommunityListAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.list-import.default");

    var response = await client.ImportCommunityListAsync(
        "list-1",
        "test-community",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/lists/list-1/import");
    Assert.Contains("\"community_slug\":\"test-community\"", handler.RequestBody!, StringComparison.Ordinal);
    Assert.Equal(1, response.Posts);
    Assert.Equal(1, response.Items);
  }

  [Fact]
  public async Task ListMutationMethodsUseExpectedRoutesAndBodies()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(ListResponseJson),
        new RecordedResponse(ListResponseJson),
        new RecordedResponse(ListResponseJson),
        new RecordedResponse(ListResponseJson),
        new RecordedResponse("{}"),
        new RecordedResponse(ListItemResponseJson),
        new RecordedResponse("{}"),
        new RecordedResponse(ListItemResponseJson),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var created = await client.CreateListAsync(
        new ListMutationBody("Reading", JsonNullableString.FromString("Saved"), "private"),
        TestContext.Current.CancellationToken);
    var fetched = await client.FetchListAsync("list 1", TestContext.Current.CancellationToken);
    var updated = await client.UpdateListAsync(
        "list 1",
        new ListMutationBody(Description: JsonNullableString.FromString("Updated")),
        TestContext.Current.CancellationToken);
    var cleared = await client.UpdateListAsync(
        "list 1",
        new ListMutationBody(Description: JsonNullableString.Null),
        TestContext.Current.CancellationToken);
    await client.DeleteListAsync("list 1", TestContext.Current.CancellationToken);
    var rssItem = await client.AddListRssFeedItemAsync("list 1", "item 1", TestContext.Current.CancellationToken);
    await client.RemoveListRssFeedItemAsync("list 1", "item 1", TestContext.Current.CancellationToken);
    var postItem = await client.AddListPostAsync("list 1", "post 1", TestContext.Current.CancellationToken);
    await client.RemoveListPostAsync("list 1", "post 1", TestContext.Current.CancellationToken);
    await client.MarkRssFeedItemReadAsync("item 1", TestContext.Current.CancellationToken);
    await client.MarkRssFeedItemUnreadAsync("item 1", TestContext.Current.CancellationToken);
    await client.MarkPostReadAsync("post 1", TestContext.Current.CancellationToken);
    await client.MarkPostUnreadAsync("post 1", TestContext.Current.CancellationToken);

    Assert.Equal("list-1", created.List.Id);
    Assert.Equal("list-1", fetched.List.Id);
    Assert.Equal("list-1", updated.List.Id);
    Assert.Equal("list-1", cleared.List.Id);
    Assert.Equal("list-item-1", rssItem.ListItem.Id);
    Assert.Equal("list-item-1", postItem.ListItem.Id);
    Assert.Collection(
        handler.Requests,
        request =>
        {
          Assert.Equal(HttpMethod.Post, request.Method);
          Assert.Equal("/api/v1/lists", request.PathAndQuery);
          Assert.Contains("\"name\":\"Reading\"", request.Body!, StringComparison.Ordinal);
        },
        request => Assert.Equal("/api/v1/lists/list%201", request.PathAndQuery),
        request =>
        {
          Assert.Equal(HttpMethod.Patch, request.Method);
          Assert.Equal("/api/v1/lists/list%201", request.PathAndQuery);
          Assert.Contains("\"description\":\"Updated\"", request.Body!, StringComparison.Ordinal);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Patch, request.Method);
          Assert.Equal("/api/v1/lists/list%201", request.PathAndQuery);
          Assert.Contains("\"description\":null", request.Body!, StringComparison.Ordinal);
        },
        request => Assert.Equal(HttpMethod.Delete, request.Method),
        request =>
        {
          Assert.Equal("/api/v1/lists/list%201/items/rss-feed-items", request.PathAndQuery);
          Assert.Contains("\"rss_feed_item_id\":\"item 1\"", request.Body!, StringComparison.Ordinal);
        },
        request => Assert.Equal("/api/v1/lists/list%201/items/rss-feed-items/item%201", request.PathAndQuery),
        request =>
        {
          Assert.Equal("/api/v1/lists/list%201/items/posts", request.PathAndQuery);
          Assert.Contains("\"post_id\":\"post 1\"", request.Body!, StringComparison.Ordinal);
        },
        request => Assert.Equal("/api/v1/lists/list%201/items/posts/post%201", request.PathAndQuery),
        request => Assert.Equal("/api/v1/rss-feed-items/item%201/read", request.PathAndQuery),
        request => Assert.Equal("/api/v1/rss-feed-items/item%201/read", request.PathAndQuery),
        request => Assert.Equal("/api/v1/posts/post%201/read", request.PathAndQuery),
        request => Assert.Equal("/api/v1/posts/post%201/read", request.PathAndQuery));
  }

  private const string ListResponseJson = """
      {
        "list": {
          "__entity_type": "list",
          "id": "list-1",
          "owner_user_id": "user-abc",
          "name": "Reading Queue",
          "description": "Articles",
          "visibility": "private",
          "created_at": "2026-06-28T10:00:00Z",
          "updated_at": "2026-06-28T10:00:00Z",
          "removed_at": null
        }
      }
      """;

  private const string ListItemResponseJson = """
      {
        "list_item": {
          "__entity_type": "list_item",
          "id": "list-item-1",
          "list_id": "list-1",
          "item_type": "rss_feed_item",
          "entity_id": "item-1",
          "order_index": 0,
          "created_at": "2026-06-28T10:05:00Z",
          "media_type": "article"
        }
      }
      """;
}
