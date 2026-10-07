using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class ApiNewsFeedService : IRssFeedItemDetailService
{
  public async Task<RssFeedItemDetail> FetchAsync(
      string itemId,
      NewsFeedItemKind kind,
      CancellationToken cancellationToken = default)
  {
    var response = await client.FetchRssFeedItemAsync(itemId, cancellationToken).ConfigureAwait(false);
    var elections = response.RssFeedItemElection is null
        ? null
        : new Dictionary<string, RssFeedItemElection>(StringComparer.Ordinal)
        {
          [response.RssFeedItem.Id] = response.RssFeedItemElection,
        };
    var votes = response.ElectionVote is null
        ? null
        : new Dictionary<string, ElectionVote>(StringComparer.Ordinal)
        {
          [response.RssFeedItem.Id] = response.ElectionVote,
        };
    var item = MapItem(
        response.RssFeedItem,
        elections,
        votes,
        response.Bookmarks,
        false,
        kind,
        response.RssFeedItemThumbnailUrl,
        response.RssFeedItemEmbeds,
        null,
        null);
    return new(item, response.ContentHtml);
  }
}
