using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class ApiNewsFeedService : IStoryRelatedArticlesService
{
  public async Task<NewsFeedPage> GetStoryRelatedArticlesPageAsync(string storyId, string primaryItemId, string? after, CancellationToken cancellationToken = default)
  {
    var response = await client.FetchStoryPageAsync(storyId, after, primaryItemId, 25, cancellationToken).ConfigureAwait(false);
    var items = response.ItemIds.Where(id => id != primaryItemId && response.RssFeedItems.ContainsKey(id))
        .Select(id => MapItem(response.RssFeedItems[id], response.RssFeedItemElections,
            response.ElectionVotes, response.RssFeedBookmarks, false, NewsFeedItemKind.Article,
            response.RssFeedItemThumbnailUrl, response.RssFeedItemEmbeds, null, null)).ToArray();
    return new(items, response.PageInfo);
  }

  private NewsFeedItem[] AttachStoryPreviews(RssFeedItemsFeedResponse response, NewsFeedItem[] items)
  {
    var previews = new Dictionary<string, StoryMemberPage?>(StringComparer.Ordinal);
    return items.Where(item =>
    {
      if (item.StoryId is not { } storyId) return true;
      if (previews.TryGetValue(storyId, out var existingPage)) return existingPage is null;
      previews[storyId] = PreviewPage(response, item);
      return true;
    }).Select(item =>
    {
      if (item.StoryId is not { } storyId || previews[storyId] is not { } page) return item;
      var peers = page.ItemIds.Where(id => id != item.Id && response.RssFeedItems.ContainsKey(id))
          .Select(id => MapItem(response.RssFeedItems[id], response.RssFeedItemElections,
              response.ElectionVotes, response.Bookmarks, false, NewsFeedItemKind.Article,
              response.RssFeedItemThumbnailUrl, response.RssFeedItemEmbeds, null, null)).ToArray();
      return item with { StoryArticles = new(storyId, item.Id, peers, page.PageInfo, localization) };
    }).ToArray();
  }

  private static StoryMemberPage? PreviewPage(RssFeedItemsFeedResponse response, NewsFeedItem item)
  {
    if (item.StoryId is not { } storyId ||
        response.StoryMemberPages?.TryGetValue(storyId, out var page) != true || page is null) return null;
    return page.PageInfo.HasNextPage || page.ItemIds.Any(id => id != item.Id && response.RssFeedItems.ContainsKey(id))
        ? page : null;
  }
}
