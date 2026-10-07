namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest Story(string storyId, string? after = null, string? excludeItemId = null, int limit = 25) =>
      Get($"/api/v1/stories/{Path(storyId)}", Query(("limit", limit), ("after", after), ("exclude_item_id", excludeItemId)));

  public static ApiRequest CreateStoryPostFromStory(string storyId, string idempotencyKey) =>
      new(HttpMethod.Post, $"/api/v1/stories/{Path(storyId)}/discussions")
      { Body = new { }, Headers = IdempotencyHeader(idempotencyKey) };

  public static ApiRequest CreateLinkPostFromRssFeedItem(string id, string idempotencyKey) =>
      new(HttpMethod.Post, $"/api/v1/rss-feed-items/{Path(id)}/discussions")
      { Body = new { }, Headers = IdempotencyHeader(idempotencyKey) };
}
