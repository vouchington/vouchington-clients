using Voucha.Client.Core.HackerNews;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel : IHackerNewsReferenceListener
{
  private IDisposable? hackerNewsSubscription;
  // Request id of a browse load suspended on the config; 0 when none is waiting.
  private int pendingHackerNewsRequest;

  // Fails closed: hidden until the backend flag has been fetched and reports references visible.
  public bool IsHackerNewsFilterVisible => browsing && hackerNewsReferences?.ReferencesVisible == true;

  public bool? HackerNewsFilter
  {
    get => activeHackerNewsFilter;
    private set => SetProperty(ref activeHackerNewsFilter, value);
  }

  private Task RefreshHackerNewsReferencesAsync(CancellationToken cancellationToken) =>
      hackerNewsReferences?.RefreshAsync(cancellationToken) ?? Task.CompletedTask;

  // Enter the loading state and drop the previous query's cursor before suspending on the config,
  // so LoadMoreAsync cannot append the old page while the filtered first page is pending.
  private async Task HoldForHackerNewsConfigAsync(int currentRequest, Task config)
  {
    endCursor = null;
    BeginLoad(currentRequest);
    Volatile.Write(ref pendingHackerNewsRequest, currentRequest);
    try { await config.ConfigureAwait(true); }
    finally { Interlocked.CompareExchange(ref pendingHackerNewsRequest, 0, currentRequest); }
  }

  void IHackerNewsReferenceListener.OnHackerNewsReferencesChanged()
  {
    var clearedFilter = !hackerNewsReferences!.ReferencesVisible && activeHackerNewsFilter is not null;
    if (clearedFilter) HackerNewsFilter = null;
    OnPropertyChanged(nameof(IsHackerNewsFilterVisible));
    // The loaded page and cursor belong to the filtered query: invalidate it and restart unfiltered.
    // A newer browse still waiting on the config drops the filter itself once it resumes; reloading
    // the previous scope here would supersede it and show the wrong scope's posts.
    var pending = Volatile.Read(ref pendingHackerNewsRequest);
    if (clearedFilter && browsing && (pending == 0 || pending != Volatile.Read(ref requestId)))
    {
      _ = LoadBrowseAsync(activePostTypes, activeTimeRange, null, activeBrowseRequest);
    }
  }
}
