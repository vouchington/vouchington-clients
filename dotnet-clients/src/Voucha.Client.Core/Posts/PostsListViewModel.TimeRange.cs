namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel
{
  public async Task SelectBrowseTimeRangeAsync(string value, CancellationToken cancellationToken = default)
  {
    if (!TimeRange.Select(value)) return;
    await LoadBrowseAsync(activePostTypes, TimeRange.RequestValue, cancellationToken, activeBrowseRequest).ConfigureAwait(true);
  }
}
