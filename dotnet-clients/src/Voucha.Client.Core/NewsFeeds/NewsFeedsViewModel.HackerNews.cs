namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  public bool IsHackerNewsFilterSelected => hasHackerNewsReferences == true;

  // Fails closed: hidden until the backend flag has been fetched and reports references visible.
  public bool IsHackerNewsFilterVisible =>
      !SelectedScope.IsSourcesScope() && hackerNewsReferences?.ReferencesVisible == true;

  public async Task ToggleHackerNewsFilterAsync(CancellationToken cancellationToken = default)
  {
    if (!IsHackerNewsFilterVisible) return;
    hasHackerNewsReferences = IsHackerNewsFilterSelected ? null : true;
    OnPropertyChanged(nameof(IsHackerNewsFilterSelected));
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  // The selected filter is only ever sent while the backend flag is on.
  private bool? EffectiveHackerNewsReferences =>
      hackerNewsReferences?.ReferencesVisible == true ? hasHackerNewsReferences : null;

  private Task RefreshHackerNewsReferencesAsync(CancellationToken cancellationToken) =>
      hackerNewsReferences?.RefreshAsync(cancellationToken) ?? Task.CompletedTask;

  private void OnHackerNewsReferencesChanged(object? sender, EventArgs e)
  {
    if (!hackerNewsReferences!.ReferencesVisible && hasHackerNewsReferences is not null)
    {
      hasHackerNewsReferences = null;
      OnPropertyChanged(nameof(IsHackerNewsFilterSelected));
      OnPropertyChanged(nameof(IsHackerNewsFilterVisible));
      // The loaded page and cursor belong to the filtered query: restart unfiltered from the top.
      _ = LoadAsync();
      return;
    }
    OnPropertyChanged(nameof(IsHackerNewsFilterVisible));
  }
}
