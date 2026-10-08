using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native news mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task ToggleSourceFollowAsync(NewsFeedItem item, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (item.Kind != NewsFeedItemKind.Source) return;
    if (!togglingSourceIds.Add(item.Id)) return;

    var previousItems = Items;
    var mutationScope = SelectedScope;
    var mutationLoadRequestId = loadRequestId;
    var following = !item.IsFollowingSource;
    Items = ToggleSourceFollow(previousItems, item.Id, following, mutationScope);
    var optimisticItems = Items;
    ErrorMessage = null;

    try
    {
      await newsFeedService.SetSourceFollowAsync(item.Id, following, cancellationToken).ConfigureAwait(true);
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
  public async Task ToggleTopicFollowAsync(NewsFeedItem item, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (item.Kind != NewsFeedItemKind.Source || item.TopicId is null) return;
    if (!togglingTopicIds.Add(item.TopicId)) return;

    var previousItems = Items;
    var mutationScope = SelectedScope;
    var mutationLoadRequestId = loadRequestId;
    var following = !item.IsFollowingTopic;
    Items = ToggleTopicFollow(previousItems, item.TopicId, following);
    var optimisticItems = Items;
    ErrorMessage = null;

    try
    {
      await newsFeedService.SetTopicFollowAsync(item.TopicId, following, cancellationToken).ConfigureAwait(true);
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
  public async Task ToggleReadAsync(NewsFeedItem item, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (item.Kind is not (NewsFeedItemKind.Article or NewsFeedItemKind.Media)) return;

    var previousItems = Items;
    var mutationScope = SelectedScope;
    var mutationLoadRequestId = loadRequestId;
    var isRead = !item.IsRead;
    if (FindStoryPeer(item.Id) is { } related)
    {
      await MutateStoryPeerAsync(related, item, peer => peer with { IsRead = isRead }, false,
          () => newsFeedService.SetReadAsync(item.Id, isRead, cancellationToken)).ConfigureAwait(true);
      return;
    }
    Items = ToggleRead(previousItems, item.Id, isRead);
    var optimisticItems = Items;
    ErrorMessage = null;

    try
    {
      await newsFeedService.SetReadAsync(item.Id, isRead, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, null);
    }
    catch (Exception ex)
    {
      RollbackOptimisticMutation(mutationScope, mutationLoadRequestId, optimisticItems, previousItems, ex.Message);
    }
  }

  private static NewsFeedItem[] ToggleSourceFollow(
      IReadOnlyList<NewsFeedItem> sourceItems,
      string sourceId,
      bool following,
      NewsFeedScope scope)
  {
    if (scope.IsSourcesScope() && scope == scope.GetKind().GetSourcesScope() && !following)
    {
      return sourceItems.Where(item => !string.Equals(item.Id, sourceId, StringComparison.Ordinal)).ToArray();
    }

    return sourceItems
        .Select(item => string.Equals(item.Id, sourceId, StringComparison.Ordinal)
            ? item with { IsFollowingSource = following }
            : item)
        .ToArray();
  }

  private static NewsFeedItem[] ToggleTopicFollow(
      IReadOnlyList<NewsFeedItem> sourceItems,
      string topicId,
      bool following)
  {
    return sourceItems
        .Select(item => string.Equals(item.TopicId, topicId, StringComparison.Ordinal)
            ? item with { IsFollowingTopic = following }
            : item)
        .ToArray();
  }

  private static NewsFeedItem[] ToggleRead(
      IReadOnlyList<NewsFeedItem> sourceItems,
      string itemId,
      bool isRead) =>
      sourceItems
          .Select(item => string.Equals(item.Id, itemId, StringComparison.Ordinal)
              ? item with { IsRead = isRead }
              : item)
          .ToArray();

  private void RollbackOptimisticMutation(
      NewsFeedScope mutationScope,
      int mutationLoadRequestId,
      IReadOnlyList<NewsFeedItem> optimisticItems,
      IReadOnlyList<NewsFeedItem> previousItems,
      string? rollbackErrorMessage)
  {
    if (SelectedScope != mutationScope || loadRequestId != mutationLoadRequestId || !ReferenceEquals(Items, optimisticItems))
    {
      return;
    }

    Items = previousItems;
    ErrorMessage = rollbackErrorMessage;
  }

}
