namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel
{
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
    await config.ConfigureAwait(true);
  }

  private void OnHackerNewsReferencesChanged(object? sender, EventArgs e)
  {
    var clearedFilter = !hackerNewsReferences!.ReferencesVisible && activeHackerNewsFilter is not null;
    if (clearedFilter) HackerNewsFilter = null;
    OnPropertyChanged(nameof(IsHackerNewsFilterVisible));
    // The loaded page and cursor belong to the filtered query: invalidate it and restart unfiltered.
    if (clearedFilter && browsing)
    {
      _ = LoadBrowseAsync(activePostTypes, activeTimeRange, null, activeBrowseRequest);
    }
  }
}
