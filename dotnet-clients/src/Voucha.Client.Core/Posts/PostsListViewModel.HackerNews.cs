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
