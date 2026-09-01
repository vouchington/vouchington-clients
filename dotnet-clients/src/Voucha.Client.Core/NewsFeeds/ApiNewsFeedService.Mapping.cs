using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class ApiNewsFeedService
{
  private NewsFeedItem[] MapItems(
      RssFeedItemsFeedResponse response,
      NewsFeedItemKind kind) =>
      response.Results
          .Select(reference => (
              ItemId: reference.EntityId ?? reference.Id,
              reference.ReadAt,
              reference.StoryId))
          .Where(reference =>
              reference.ItemId is { } itemId &&
              response.RssFeedItems.ContainsKey(itemId))
          .Select(reference => MapItem(
              response.RssFeedItems[reference.ItemId!],
              response.RssFeedItemElections,
              response.ElectionVotes,
              response.Bookmarks,
              reference.ReadAt is not null,
              kind,
              response.RssFeedItemThumbnailUrl,
              response.RssFeedItemEmbeds,
              reference.StoryId,
              response.StoryMemberIds,
              response.StoryPostIds))
          .ToArray();

  private NewsFeedItem MapItem(
      RssFeedItem item,
      IReadOnlyDictionary<string, RssFeedItemElection>? elections,
      IReadOnlyDictionary<string, ElectionVote>? votes,
      IReadOnlyDictionary<string, BookmarkPredicates>? bookmarks,
      bool isRead,
      NewsFeedItemKind kind,
      IReadOnlyDictionary<string, string>? thumbnailUrls,
      IReadOnlyDictionary<string, UrlEmbed>? embeds,
      string? storyId,
      IReadOnlyDictionary<string, IReadOnlyList<string>>? storyMemberIds,
      IReadOnlyDictionary<string, string>? storyPostIds)
  {
    var title = item.Data?.Title ?? item.Title
        ?? localization.Localize(UiMessageKey.NativeDotnetNewsFeedsUntitled);
    var source = item.RssFeed?.Title
        ?? localization.Localize(UiMessageKey.NativeDotnetNewsFeedsNews);
    var summary = item.Data?.ContentSnippet
        ?? item.Description
        ?? item.Data?.Description
        ?? string.Empty;
    var mediaUrl = NormalizeMediaUrl(
        item.EnclosureUrl ?? item.Data?.EnclosureUrl ?? item.MediaContent?.Url);
    string? thumbnailUrl = null;
    thumbnailUrls?.TryGetValue(item.Id, out thumbnailUrl);
    UrlEmbed? embed = null;
    embeds?.TryGetValue(item.Id, out embed);
    RssFeedItemElection? election = null;
    elections?.TryGetValue(item.Id, out election);

    ElectionVote? vote = null;
    votes?.TryGetValue(item.Id, out vote);
    var storyPeerCount = 0;
    if (storyId is not null &&
        storyMemberIds?.TryGetValue(storyId, out var memberIds) == true)
    {
      storyPeerCount = memberIds.Count(memberId =>
          !string.Equals(memberId, item.Id, StringComparison.Ordinal));
    }

    string? storyPostId = null;
    if (storyId is not null)
    {
      storyPostIds?.TryGetValue(storyId, out storyPostId);
      if (storyPostId is null)
      {
        fallbackStoryDiscussionPostIds.TryGetValue(storyId, out storyPostId);
      }
    }
    return new NewsFeedItem(
        item.Id,
        title,
        source,
        summary,
        item.Link ?? item.Url?.Url ?? item.Data?.Link,
        item.PublishedAt,
        kind,
        IsRead: isRead,
        IsSaved: BookmarkSidecar.IsActive(bookmarks, item.Id, BookmarkPredicate.Save),
        IsHidden: BookmarkSidecar.IsActive(bookmarks, item.Id, BookmarkPredicate.Hide),
        VoteScoreNet: election?.VotesScoreNet,
        VoteCountUp: election?.VotesCountUp,
        VoteCountDown: election?.VotesCountDown,
        CurrentVoteChoice: vote?.Choice,
        MediaUrl: mediaUrl,
        ThumbnailUrl: SelectThumbnailUrl(item.Data?.ThumbnailUrl, thumbnailUrl),
        ProtocolMediaType: SelectProtocolMediaType(item),
        VideoPlatform: item.VideoPlatform ?? item.Data?.VideoPlatform,
        VideoId: item.VideoId ?? item.Data?.VideoId,
        DurationSeconds: item.DurationSeconds
            ?? item.Data?.DurationSeconds
            ?? item.MediaContent?.Duration,
        StoryId: storyId,
        StoryPeerCount: storyPeerCount,
        StoryPostId: storyPostId,
        EmbedPreview: UrlEmbedPreviews.From(embed),
        Localization: localization);
  }

  private static string? SelectProtocolMediaType(RssFeedItem item) =>
      item.MediaType
      ?? item.Data?.MediaType
      ?? SelectFallbackProtocolMediaType(
          item.Data?.EnclosureType,
          item.MediaContent?.Medium,
          item.MediaContent?.Type);

  private static string? SelectFallbackProtocolMediaType(params string?[] candidates) =>
      candidates.FirstOrDefault(IsClassifiedProtocolMediaType)
      ?? candidates.FirstOrDefault(static candidate => !string.IsNullOrWhiteSpace(candidate));

  private static bool IsClassifiedProtocolMediaType(string? mediaType) =>
      NewsFeedItem.IsProtocolMediaType(mediaType, "audio") ||
      NewsFeedItem.IsProtocolMediaType(mediaType, "video");
}
