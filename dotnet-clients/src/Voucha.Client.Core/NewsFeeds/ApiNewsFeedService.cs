using System.Collections.Concurrent;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class ApiNewsFeedService : INewsFeedService, IStoryDiscussionService, INewsFeedSessionState
{
  private readonly VouchaApiClient client;
  private readonly IUiLocalization localization;
  private readonly ConcurrentDictionary<string, string> fallbackStoryDiscussionPostIds = new(StringComparer.Ordinal);
  private string? identityUserId;

  public ApiNewsFeedService(VouchaApiClient client, IUiLocalization? localization = null)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.localization = localization ?? UiLocalization.English;
  }

  public void ResetSessionState()
  {
    identityUserId = null;
    fallbackStoryDiscussionPostIds.Clear();
  }

  public async Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
      NewsFeedScope scope,
      CancellationToken cancellationToken = default) =>
      (await GetNewsFeedPageAsync(
          scope,
          scope.IsSourcesScope() ? scope.GetDefaultSourceFeedType() : NewsFeedSourceType.Article,
          limit: scope.IsSourcesScope() ? 25 : 20,
          cancellationToken: cancellationToken).ConfigureAwait(false)).Items;

  public async Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType,
      CancellationToken cancellationToken = default)
    => (await GetNewsFeedPageAsync(
        scope,
        sourceFeedType,
        limit: scope.IsSourcesScope() ? 25 : 20,
        cancellationToken: cancellationToken).ConfigureAwait(false)).Items;

  public Task<NewsFeedPage> GetNewsFeedPageAsync(
      NewsFeedScope scope,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default) =>
      GetNewsFeedPageAsync(
          scope,
          scope.IsSourcesScope() ? scope.GetDefaultSourceFeedType() : NewsFeedSourceType.Article,
          after,
          limit,
          cancellationToken);

  public async Task<NewsFeedPage> GetNewsFeedPageAsync(
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default)
  {
    if (scope.IsSourcesScope())
    {
      return IsAllSourcesScope(scope)
          ? await FetchAllSourcesPageAsync(sourceFeedType, after, limit, cancellationToken).ConfigureAwait(false)
          : await FetchYourSourcesPageAsync(sourceFeedType, after, limit, cancellationToken).ConfigureAwait(false);
    }

    var mediaType = scope.GetMediaType();
    var kind = scope.GetKind() == NewsFeedKind.News ? NewsFeedItemKind.Article : NewsFeedItemKind.Media;
    return IsAllItemsScope(scope)
        ? await FetchAllNewsPageAsync(mediaType, kind, after, limit, cancellationToken).ConfigureAwait(false)
        : await FetchFeedPageAsync("any", mediaType, kind, after, limit, cancellationToken).ConfigureAwait(false);
  }

  public Task SetSourceFollowAsync(
      string sourceId,
      bool following,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(
          following ? VouchaApiEndpoints.FollowRssFeed(sourceId) : VouchaApiEndpoints.UnfollowRssFeed(sourceId),
          cancellationToken);

  public Task SetTopicFollowAsync(
      string topicId,
      bool following,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(
          following
              ? VouchaApiEndpoints.Bookmark("topic", topicId, "follow")
              : VouchaApiEndpoints.Unbookmark("topic", topicId, "follow"),
          cancellationToken);

  public Task SetReadAsync(
      string itemId,
      bool read,
      CancellationToken cancellationToken = default) =>
      read
          ? client.MarkRssFeedItemReadAsync(itemId, cancellationToken)
          : client.MarkRssFeedItemUnreadAsync(itemId, cancellationToken);

  public Task VoteRssFeedItemAsync(
      string itemId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      client.VoteRssFeedItemAsync(itemId, choice, cancellationToken);

  public Task ClearRssFeedItemVoteAsync(string itemId, CancellationToken cancellationToken = default) =>
      client.ClearRssFeedItemVoteAsync(itemId, cancellationToken);

  public Task VoteTopicAsync(
      string topicId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      client.VoteTopicAsync(topicId, choice, cancellationToken);

  public Task ClearTopicVoteAsync(string topicId, CancellationToken cancellationToken = default) =>
      client.ClearTopicVoteAsync(topicId, cancellationToken);

  private static bool IsAllItemsScope(NewsFeedScope scope) => scope is NewsFeedScope.AllNews or NewsFeedScope.AllPodcasts or NewsFeedScope.AllVideos;

  private static bool IsAllSourcesScope(NewsFeedScope scope) => scope is NewsFeedScope.AllSources or NewsFeedScope.AllPodcastSources or NewsFeedScope.AllVideoSources;

  private async Task<NewsFeedPage> FetchAllNewsPageAsync(
      string mediaType,
      NewsFeedItemKind kind,
      string? after,
      int limit,
      CancellationToken cancellationToken)
  {
    var response = await client.FetchAllRssFeedItemsAsync(
        after,
        limit,
        mediaType: mediaType,
        cancellationToken: cancellationToken).ConfigureAwait(false);
    return new(MapItems(response, kind), response.PageInfo);
  }

  private async Task<NewsFeedPage> FetchFeedPageAsync(
      string feedType,
      string mediaType,
      NewsFeedItemKind kind,
      string? after,
      int limit,
      CancellationToken cancellationToken)
  {
    var response = await client.FetchRssFeedItemsAsync(
        new FetchRssFeedItemsRequest(feedType, MediaType: mediaType, After: after, Limit: limit),
        cancellationToken).ConfigureAwait(false);
    return new(MapItems(response, kind), response.PageInfo);
  }

}
