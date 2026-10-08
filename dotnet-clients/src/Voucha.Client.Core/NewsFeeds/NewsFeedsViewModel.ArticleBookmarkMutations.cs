using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
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
    var isStoryPrimaryHide = predicate == BookmarkPredicate.Hide && active && removeOnActivate && item.StoryId is not null;
    var isStoryPrimaryUnhide = predicate == BookmarkPredicate.Hide && !active && item.StoryId is not null;
    var previousIndex = isStoryPrimaryHide
        ? previousItems.ToList().FindIndex(row => row.Id == item.Id)
        : -1;
    var restoreStoryPrimary = previousIndex >= 0;
    if (isStoryPrimaryHide) BeginStoryPrimarySuppression(item.Id, mutationLoadRequestId);
    Items = ToggleArticleBookmark(previousItems, item.Id, predicate, active, removeOnActivate);
    var optimisticItems = Items;
    ErrorMessage = null;

    try
    {
      await bookmarkService.SetAsync("rss_feed_item", item.Id, predicate, active, cancellationToken)
          .ConfigureAwait(true);
      if (isStoryPrimaryHide) CompleteStoryPrimarySuppression(item.Id, mutationLoadRequestId);
      else if (isStoryPrimaryUnhide) ClearStoryPrimarySuppression(item.Id, mutationLoadRequestId);
    }
    catch (OperationCanceledException)
    {
      var suppressedRows = isStoryPrimaryHide ? ReleaseStoryPrimarySuppression(item.Id, mutationLoadRequestId) : [];
      RollbackArticleBookmark(
          mutationScope, mutationLoadRequestId, optimisticItems, previousItems, item, previousIndex, restoreStoryPrimary, null, suppressedRows);
    }
    catch (Exception ex)
    {
      var suppressedRows = isStoryPrimaryHide ? ReleaseStoryPrimarySuppression(item.Id, mutationLoadRequestId) : [];
      RollbackArticleBookmark(
          mutationScope, mutationLoadRequestId, optimisticItems, previousItems, item, previousIndex, restoreStoryPrimary, ex.Message, suppressedRows);
    }
    finally
    {
      togglingArticleIds.Remove(item.Id);
    }
  }

  private void RollbackArticleBookmark(
      NewsFeedScope mutationScope,
      int mutationLoadRequestId,
      IReadOnlyList<NewsFeedItem> optimisticItems,
      IReadOnlyList<NewsFeedItem> previousItems,
      NewsFeedItem originalItem,
      int previousIndex,
      bool restoreStoryPrimary,
      string? rollbackErrorMessage,
      IReadOnlyList<NewsFeedItem>? suppressedRows = null)
  {
    if (!restoreStoryPrimary)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, rollbackErrorMessage);
      return;
    }

    RollbackRemovedStoryPrimary(
        mutationScope, mutationLoadRequestId, originalItem, previousIndex, rollbackErrorMessage, suppressedRows);
  }

  private void RollbackRemovedStoryPrimary(
      NewsFeedScope mutationScope,
      int mutationLoadRequestId,
      NewsFeedItem originalPrimary,
      int originalIndex,
      string? rollbackErrorMessage,
      IReadOnlyList<NewsFeedItem>? suppressedRows)
  {
    if (SelectedScope != mutationScope || loadRequestId != mutationLoadRequestId || originalPrimary.StoryId is not { } storyId)
    {
      return;
    }

    var sameStoryRows = Items.Where(item => item.StoryId == storyId)
        .Concat(suppressedRows?.Where(item => item.StoryId == storyId) ?? [])
        .ToArray();
    var relatedGroups = sameStoryRows.Select(item => item.StoryArticles).OfType<StoryRelatedArticles>().Distinct().ToArray();
    var originalGroup = originalPrimary.StoryArticles;
    StoryRelatedArticles? restoredGroup = originalGroup;

    if (originalGroup is not null)
    {
      foreach (var incomingGroup in relatedGroups)
        originalGroup.IncludeContinuation(incomingGroup);
      originalGroup.IncludePeers(sameStoryRows.SelectMany(PeersFor).Where(item => item.Id != originalPrimary.Id));
    }
    else if (relatedGroups.FirstOrDefault() is { } incomingGroup)
    {
      restoredGroup = incomingGroup.WithPrimary(
          originalPrimary,
          sameStoryRows.SelectMany(PeersFor));
      foreach (var additionalGroup in relatedGroups.Skip(1))
        restoredGroup!.IncludePeers(additionalGroup.Items);
    }
    else if (sameStoryRows.Length > 0)
    {
      restoredGroup = new StoryRelatedArticles(
          storyId,
          originalPrimary.Id,
          sameStoryRows,
          new(null, false, null),
          originalPrimary.Localization ?? UiLocalization.English);
    }

    var restoredPrimary = originalPrimary with { StoryArticles = restoredGroup };
    var restoredItems = Items.Where(item => item.StoryId != storyId).ToList();
    restoredItems.Insert(Math.Clamp(originalIndex, 0, restoredItems.Count), restoredPrimary);
    Items = restoredItems;
    ErrorMessage = rollbackErrorMessage;

    IEnumerable<NewsFeedItem> PeersFor(NewsFeedItem item)
    {
      if (item.Id != originalPrimary.Id) yield return item;
      if (item.StoryArticles is not { } group) yield break;
      foreach (var peer in group.Items) yield return peer;
    }
  }
}
