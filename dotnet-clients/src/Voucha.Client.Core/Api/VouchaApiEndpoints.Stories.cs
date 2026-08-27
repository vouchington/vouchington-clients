namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CreateStoryPostFromStory(string storyId) =>
      new(HttpMethod.Post, $"/api/v1/stories/{Path(storyId)}/discussions") { Body = new { } };

  public static ApiRequest CreateLinkPostFromRssFeedItem(string id) =>
      new(HttpMethod.Post, $"/api/v1/rss-feed-items/{Path(id)}/discussions") { Body = new { } };
}
