namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest RssFeedItem(string id) => Get($"/api/v1/rss-feed-items/{Path(id)}");

  public static ApiRequest PodcastEpisodeChapters(string id) => Get($"/api/v1/podcast-episodes/{Path(id)}/chapters");
}
