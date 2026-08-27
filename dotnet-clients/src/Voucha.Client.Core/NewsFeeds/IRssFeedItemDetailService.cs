namespace Voucha.Client.Core.NewsFeeds;

public interface IRssFeedItemDetailService
{
  Task<RssFeedItemDetail> FetchAsync(
      string itemId,
      NewsFeedItemKind kind,
      CancellationToken cancellationToken = default);
}

public sealed record RssFeedItemDetail(NewsFeedItem Item, string? ContentHtml);
