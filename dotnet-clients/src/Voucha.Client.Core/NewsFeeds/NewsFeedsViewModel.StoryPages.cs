using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  private readonly Dictionary<string, SuppressedStoryPrimary> suppressedStoryPrimaries = new(StringComparer.Ordinal);
  private int suppressionLoadRequestId = -1;

  public void ToggleStoryArticles(NewsFeedItem item)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (!Items.Contains(item)) return;
    if (item.StoryArticles is { } related) related.IsExpanded = !related.IsExpanded;
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types",
      Justification = "Related article loading retains content and exposes a retryable page error to the native UI.")]
  public async Task LoadMoreStoryArticlesAsync(NewsFeedItem item, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (newsFeedService is not IStoryRelatedArticlesService service || item.StoryArticles is not { } related) return;
    var request = related.BeginNextPage();
    if (request is null) return;
    var generation = Volatile.Read(ref loadRequestId);
    try
    {
      var page = await service.GetStoryRelatedArticlesPageAsync(related.StoryId, related.PrimaryItemId, request.Cursor, cancellationToken).ConfigureAwait(true);
      if (generation != Volatile.Read(ref loadRequestId) || !Items.Any(row => ReferenceEquals(row.StoryArticles, related)))
      {
        related.Cancel(request);
        return;
      }
      if (related.Complete(request, page) &&
          page.StoryPostIds?.TryGetValue(related.StoryId, out var postId) == true &&
          !string.IsNullOrWhiteSpace(postId))
        Items = SetStoryDiscussionPost(Items, related.StoryId, postId);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      related.Cancel(request);
    }
    catch (Exception ex)
    {
      related.Fail(request, ex.Message);
    }
  }

  private NewsFeedItem[] KeepDisplayedStoryPrimaries(IReadOnlyList<NewsFeedItem> incoming)
  {
    var grouped = Items.Where(item => item.StoryId is not null && item.StoryArticles is not null)
        .GroupBy(item => item.StoryId!, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().StoryArticles!, StringComparer.Ordinal);
    var ungrouped = Items.Where(item => item.StoryId is not null && item.StoryArticles is null)
        .GroupBy(item => item.StoryId!, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    var kept = new List<NewsFeedItem>();
    foreach (var item in incoming)
    {
      if (IsStoryPrimarySuppressed(item.Id))
      {
        CaptureSuppressedStoryPrimaryRow(item);
        continue;
      }
      if (item.StoryId is not { } storyId)
      {
        kept.Add(item);
        continue;
      }
      if (grouped.TryGetValue(storyId, out var existingGroup))
      {
        if (item.StoryArticles is { } preview) existingGroup.IncludeContinuation(preview);
        existingGroup.IncludePeers(item.StoryArticles is { } previewPeers
            ? [item, .. previewPeers.Items]
            : [item]);
        continue;
      }
      if (item.StoryArticles is not { } incomingGroup)
      {
        kept.Add(item);
        ungrouped.TryAdd(storyId, item);
        continue;
      }
      if (!ungrouped.TryGetValue(storyId, out var first))
      {
        kept.Add(item);
        grouped.Add(storyId, incomingGroup);
        continue;
      }

      var priorPeers = Items.Concat(kept).Where(row => row.StoryId == storyId && row.Id != first.Id);
      var promoted = incomingGroup.WithPrimary(first, [item, .. priorPeers]);
      grouped.Add(storyId, promoted);
      ungrouped.Remove(storyId);
      if (Items.Any(row => row.FeedRowId == first.FeedRowId))
        Items = Items.Where(row => row.StoryId != storyId || row.Id == first.Id || row.StoryArticles is not null)
            .Select(row => row.FeedRowId == first.FeedRowId ? row with { StoryArticles = promoted } : row).ToArray();
      kept.RemoveAll(row => row.StoryId == storyId && row.Id != first.Id);
      for (var index = 0; index < kept.Count; index++)
        if (kept[index].FeedRowId == first.FeedRowId) kept[index] = kept[index] with { StoryArticles = promoted };
    }
    return [.. kept];
  }

  private void BeginStoryPrimarySuppression(string itemId, int generation)
  {
    EnsureSuppressionGeneration();
    if (generation != suppressionLoadRequestId) return;
    suppressedStoryPrimaries[itemId] = new();
  }

  private void CompleteStoryPrimarySuppression(string itemId, int generation)
  {
    if (!TryGetCurrentSuppression(itemId, generation, out var suppression)) return;
    suppression.IsPending = false;
    suppression.ReturnedRows.Clear();
  }

  private void ClearStoryPrimarySuppression(string itemId, int generation)
  {
    if (!TryGetCurrentSuppression(itemId, generation, out _)) return;
    suppressedStoryPrimaries.Remove(itemId);
  }

  private IReadOnlyList<NewsFeedItem> ReleaseStoryPrimarySuppression(string itemId, int generation)
  {
    if (!TryGetCurrentSuppression(itemId, generation, out var suppression)) return [];
    suppressedStoryPrimaries.Remove(itemId);
    return [.. suppression.ReturnedRows];
  }

  private bool IsStoryPrimarySuppressed(string itemId)
  {
    EnsureSuppressionGeneration();
    return suppressedStoryPrimaries.ContainsKey(itemId);
  }

  private void CaptureSuppressedStoryPrimaryRow(NewsFeedItem item)
  {
    EnsureSuppressionGeneration();
    if (!suppressedStoryPrimaries.TryGetValue(item.Id, out var suppression) || !suppression.IsPending) return;
    suppression.ReturnedRows.Add(item);
  }

  private bool TryGetCurrentSuppression(string itemId, int generation, out SuppressedStoryPrimary suppression)
  {
    EnsureSuppressionGeneration();
    if (generation == suppressionLoadRequestId && suppressedStoryPrimaries.TryGetValue(itemId, out suppression!)) return true;
    suppression = null!;
    return false;
  }

  private void EnsureSuppressionGeneration()
  {
    var generation = Volatile.Read(ref loadRequestId);
    if (generation == suppressionLoadRequestId) return;
    suppressedStoryPrimaries.Clear();
    suppressionLoadRequestId = generation;
  }

  private sealed class SuppressedStoryPrimary
  {
    public bool IsPending { get; set; } = true;
    public List<NewsFeedItem> ReturnedRows { get; } = [];
  }
}
