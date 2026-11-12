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
    if (!hackerNewsReferences!.ReferencesVisible) HackerNewsFilter = null;
    OnPropertyChanged(nameof(IsHackerNewsFilterVisible));
  }
}
