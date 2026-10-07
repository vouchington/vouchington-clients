using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native news mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task ToggleSaveAsync(NewsFeedItem item, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (!item.HasItemActions) return;
    await ToggleArticleBookmarkAsync(item, BookmarkPredicate.Save, removeOnActivate: false, cancellationToken)
        .ConfigureAwait(true);
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native news mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task ToggleHideAsync(NewsFeedItem item, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (!item.HasItemActions) return;
    await ToggleArticleBookmarkAsync(item, BookmarkPredicate.Hide, removeOnActivate: true, cancellationToken)
        .ConfigureAwait(true);
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native news mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task ToggleSourceMuteAsync(NewsFeedItem item, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (item.Kind != NewsFeedItemKind.Source) return;
    if (!togglingSourceIds.Add(item.Id)) return;

    var previousItems = Items;
    var mutationScope = SelectedScope;
    var mutationLoadRequestId = loadRequestId;
    var muted = !item.IsMutedSource;
    Items = ToggleSourceBookmark(previousItems, item.Id, muted, mutationScope, removeOnActivate: true);
    var optimisticItems = Items;
    ErrorMessage = null;

    try
    {
      await bookmarkService.SetAsync("rss_feed", item.Id, BookmarkPredicate.Mute, muted, cancellationToken)
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, null);
    }
    catch (Exception ex)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, ex.Message);
    }
    finally
    {
      togglingSourceIds.Remove(item.Id);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native news mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task ToggleTopicMuteAsync(NewsFeedItem item, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (item.Kind != NewsFeedItemKind.Source || item.TopicId is null) return;
    if (!togglingTopicIds.Add(item.TopicId)) return;

    var previousItems = Items;
    var mutationScope = SelectedScope;
    var mutationLoadRequestId = loadRequestId;
    var muted = !item.IsMutedTopic;
    Items = ToggleTopicBookmark(previousItems, item.TopicId, muted);
    var optimisticItems = Items;
    ErrorMessage = null;

    try
    {
      await bookmarkService.SetAsync("topic", item.TopicId, BookmarkPredicate.Mute, muted, cancellationToken)
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, null);
    }
    catch (Exception ex)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, ex.Message);
    }
    finally
    {
      togglingTopicIds.Remove(item.TopicId);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native news mutations surface API failures in view state before MAUI async event handlers observe them.")]
  private async Task ToggleArticleBookmarkAsync(
      NewsFeedItem item,
      BookmarkPredicate predicate,
      bool removeOnActivate,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (FindStoryPeer(item.Id) is { } related)
    {
      var enabled = predicate == BookmarkPredicate.Save ? !item.IsSaved : !item.IsHidden;
      await MutateStoryPeerAsync(related, item,
          peer => peer with
          {
            IsSaved = predicate == BookmarkPredicate.Save ? enabled : peer.IsSaved,
            IsHidden = predicate == BookmarkPredicate.Hide ? enabled : peer.IsHidden
          },
          predicate == BookmarkPredicate.Hide && enabled && removeOnActivate,
          () => bookmarkService.SetAsync("rss_feed_item", item.Id, predicate, enabled, cancellationToken)).ConfigureAwait(true);
      return;
    }
    if (!togglingArticleIds.Add(item.Id)) return;

    var previousItems = Items;
    var mutationScope = SelectedScope;
    var mutationLoadRequestId = loadRequestId;
    var active = predicate == BookmarkPredicate.Save ? !item.IsSaved : !item.IsHidden;
    Items = ToggleArticleBookmark(previousItems, item.Id, predicate, active, removeOnActivate);
    var optimisticItems = Items;
    ErrorMessage = null;

    try
    {
      await bookmarkService.SetAsync("rss_feed_item", item.Id, predicate, active, cancellationToken)
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, null);
    }
    catch (Exception ex)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, ex.Message);
    }
    finally
    {
      togglingArticleIds.Remove(item.Id);
    }
  }

  private static NewsFeedItem[] ToggleSourceBookmark(
      IReadOnlyList<NewsFeedItem> sourceItems,
      string sourceId,
      bool muted,
      NewsFeedScope scope,
      bool removeOnActivate)
  {
    if (scope.IsSourcesScope() && scope == scope.GetKind().GetSourcesScope() && muted && removeOnActivate)
    {
      return sourceItems.Where(item => !string.Equals(item.Id, sourceId, StringComparison.Ordinal)).ToArray();
    }

    return sourceItems
        .Select(item => string.Equals(item.Id, sourceId, StringComparison.Ordinal)
            ? item with { IsMutedSource = muted, IsFollowingSource = muted ? false : item.IsFollowingSource }
            : item)
        .ToArray();
  }

  private static NewsFeedItem[] ToggleTopicBookmark(
      IReadOnlyList<NewsFeedItem> sourceItems,
      string topicId,
      bool muted) =>
      sourceItems
          .Select(item => string.Equals(item.TopicId, topicId, StringComparison.Ordinal)
              ? item with { IsMutedTopic = muted, IsFollowingTopic = muted ? false : item.IsFollowingTopic }
              : item)
          .ToArray();
}
