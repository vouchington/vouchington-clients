using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  public static void ToggleStoryArticles(NewsFeedItem item)
  {
    ArgumentNullException.ThrowIfNull(item);
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
      if (generation != Volatile.Read(ref loadRequestId) || !Items.Any(row => ReferenceEquals(row.StoryArticles, related))) return;
      related.Complete(request, page);
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
    var displayed = Items.Where(item => item.StoryArticles is not null)
        .Select(item => item.StoryId!).ToHashSet(StringComparer.Ordinal);
    return incoming.Where(item => item.StoryId is not { } storyId || displayed.Add(storyId)).ToArray();
  }
}
