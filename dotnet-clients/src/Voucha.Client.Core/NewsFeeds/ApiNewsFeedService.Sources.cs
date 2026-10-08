using Voucha.Client.Core.Api;
using Voucha.Client.Core.Voting;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class ApiNewsFeedService
{
  private async Task<NewsFeedPage> FetchAllSourcesPageAsync(
      NewsFeedSourceType sourceFeedType,
      string? after,
      int limit,
      CancellationToken cancellationToken)
  {
    var response = await client.FetchRssFeedsAsync(
        new FetchRssFeedsRequest(FeedType: sourceFeedType.ApiValue(), After: after, Limit: limit),
        cancellationToken).ConfigureAwait(false);
    return new(MapSources(response), response.PageInfo);
  }

  private async Task<NewsFeedPage> FetchYourSourcesPageAsync(
      NewsFeedSourceType sourceFeedType,
      string? after,
      int limit,
      CancellationToken cancellationToken)
  {
    var userId = identityUserId;
    try
    {
      if (userId is null)
      {
        var identity = await client.FetchMyIdentityAsync(cancellationToken).ConfigureAwait(false);
        userId = identity.Identity.Id;
        identityUserId = userId;
      }
    }
    catch (VouchaApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    {
      return new([], new PageInfo(null, false, null));
    }

    var response = await client.FetchUserRssFeedsAsync(
        new FetchUserRssFeedsRequest(
            userId,
            FeedType: sourceFeedType.ApiValue(),
            After: after,
            Limit: limit),
        cancellationToken).ConfigureAwait(false);
    return new(MapSources(response, forceFollowingSource: true), response.PageInfo);
  }

  private NewsFeedItem[] MapSources(RssFeedsResponse response, bool forceFollowingSource = false) =>
      response.Results.Select(source =>
      {
        var isFollowingSource = forceFollowingSource ||
            BookmarkSidecar.IsActive(response.Bookmarks, source.Id, BookmarkPredicate.Follow);
        var isMutedSource = BookmarkSidecar.IsActive(response.Bookmarks, source.Id, BookmarkPredicate.Mute);
        var topicId = source.Topic?.Id;
        var election = ElectionSidecars.TopicElectionFor(response.TopicElections, topicId);
        var vote = ElectionSidecars.ElectionVoteFor(response.ElectionVotes, topicId);

        return new NewsFeedItem(
            source.Id,
            source.Title,
            source.Hostname?.Hostname ?? source.RssFeedUrl?.Url?.Host
                ?? localization.Localize(UiMessageKey.NativeDotnetNewsFeedsSource),
            source.FeedType?.SourceLabel(localization)
                ?? localization.Localize(UiMessageKey.NativeDotnetNewsFeedsSource),
            source.RssFeedUrl?.Url,
            source.LastFetchedAt ?? DateTimeOffset.MinValue,
            NewsFeedItemKind.Source,
            topicId,
            isFollowingSource,
            topicId is not null && BookmarkSidecar.IsActive(response.Bookmarks, topicId, BookmarkPredicate.Follow),
            isMutedSource,
            topicId is not null && BookmarkSidecar.IsActive(response.Bookmarks, topicId, BookmarkPredicate.Mute),
            VoteScoreNet: election?.VotesScoreNet,
            VoteCountUp: election?.VotesCountUp,
            VoteCountDown: election?.VotesCountDown,
            CurrentVoteChoice: vote?.Choice,
            Provenance: source.Provenance);
      }).ToArray();

  private string FeedTypeLabel(string? feedType) =>
      feedType switch
      {
        "article" => localization.Localize(UiMessageKey.NativeDotnetNewsFeedsNewsSource),
        "podcast" => localization.Localize(UiMessageKey.NativeDotnetNewsFeedsPodcastSource),
        "video" => localization.Localize(UiMessageKey.NativeDotnetNewsFeedsVideoSource),
        _ => localization.Localize(UiMessageKey.NativeDotnetNewsFeedsSource),
      };

  private static Uri? NormalizeMediaUrl(Uri? url) =>
      url is { Scheme: "http", Host: not null, IsAbsoluteUri: true }
          ? new UriBuilder(url)
          {
            Scheme = Uri.UriSchemeHttps,
            Port = url.IsDefaultPort ? -1 : url.Port,
          }.Uri
          : url;
}
