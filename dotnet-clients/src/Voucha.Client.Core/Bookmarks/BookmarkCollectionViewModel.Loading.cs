using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.Core.Bookmarks;

public sealed partial class BookmarkCollectionViewModel
{
  private async Task<BookmarkCollectionLoadResult> LoadCollectionRowsAsync(
      string userId,
      BookmarkCollectionRouteContext route,
      CancellationToken cancellationToken)
  {
    return route.Kind switch
    {
      BookmarkCollectionKind.Posts => await LoadPostRowsAsync(userId, route, cancellationToken).ConfigureAwait(true),
      BookmarkCollectionKind.Topics => Result((await client.FetchUserTopicsCollectionAsync(
              userId,
              route.ListType,
              cancellationToken: cancellationToken).ConfigureAwait(true)).Results
          .Select((topic, rank) => BookmarkCollectionRowFactory.Topic(topic, route.InverseAction, rank, localization))
          .ToArray()),
      BookmarkCollectionKind.Users => Result((await client.FetchUserUsersCollectionAsync(
              userId,
              route.ListType,
              cancellationToken: cancellationToken).ConfigureAwait(true)).Results
          .Select((user, rank) => BookmarkCollectionRowFactory.User(user, route.InverseAction, rank, localization))
          .ToArray()),
      BookmarkCollectionKind.RssFeedItems => RssItemResult(await client.FetchUserRssFeedItemsCollectionAsync(
              userId,
              route.ListType,
              route.MediaType,
              cancellationToken: cancellationToken).ConfigureAwait(true), route),
      BookmarkCollectionKind.RssFeeds => Result((await client.FetchUserRssFeedsAsync(
              new FetchUserRssFeedsRequest(userId, route.ListType, route.FeedType),
              cancellationToken).ConfigureAwait(true)).Results
          .Select((feed, rank) => BookmarkCollectionRowFactory.RssFeed(feed, route.InverseAction, rank, localization))
          .ToArray()),
      BookmarkCollectionKind.Urls => Result((await client.FetchUserUrlsAsync(
              userId,
              route.ListType,
              cancellationToken: cancellationToken).ConfigureAwait(true)).Results
          .Select((url, rank) => BookmarkCollectionRowFactory.Url(url, route.InverseAction, rank, localization))
          .ToArray()),
      BookmarkCollectionKind.Hostnames => Result((await client.FetchUserHostnamesAsync(
              userId,
              route.ListType,
              cancellationToken: cancellationToken).ConfigureAwait(true)).Results
          .Select((hostname, rank) => BookmarkCollectionRowFactory.Hostname(hostname, route.InverseAction, rank, localization))
          .ToArray()),
      BookmarkCollectionKind.Communities => Result((await client.FetchUserCommunitiesCollectionAsync(
              userId,
              route.ListType,
              cancellationToken: cancellationToken).ConfigureAwait(true)).Results
          .Select((community, rank) => BookmarkCollectionRowFactory.Community(community, route.InverseAction, rank, localization))
          .ToArray()),
      _ => Result([]),
    };
  }

  private async Task<BookmarkCollectionLoadResult> LoadPostRowsAsync(
      string userId,
      BookmarkCollectionRouteContext route,
      CancellationToken cancellationToken)
  {
    var response = await client.FetchUserPostsCollectionAsync(
        userId,
        route.ListType,
        cancellationToken: cancellationToken).ConfigureAwait(true);
    return new(
        response.Results.Select((post, rank) =>
        {
          UrlEmbed? embed = null;
          response.PostLinkEmbeds?.TryGetValue(post.Id, out embed);
          return BookmarkCollectionRowFactory.Post(post, route.InverseAction, rank, embed, localization);
        }).ToArray(),
        response.PageInfo);
  }

  private static BookmarkCollectionLoadResult Result(IReadOnlyList<BookmarkCollectionRow> rows) => new(rows, null);

  private BookmarkCollectionLoadResult RssItemResult(
      BookmarkCollectionResponse<RssFeedItem> response,
      BookmarkCollectionRouteContext route) =>
      new(response.Results.Select((item, rank) =>
      {
        UrlEmbed? embed = null;
        response.RssFeedItemEmbeds?.TryGetValue(item.Id, out embed);
        return BookmarkCollectionRowFactory.RssItem(item, route.InverseAction, rank, embed, localization);
      }).ToArray(), response.PageInfo);

  private sealed record BookmarkCollectionLoadResult(IReadOnlyList<BookmarkCollectionRow> Rows, PageInfo? PageInfo);
}
