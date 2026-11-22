using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private int? imageLimit;

  /// Each load bumps this, so a response from an earlier load cannot overwrite a newer cap.
  private int imageLimitGeneration;

  /// <summary>The server-configured image cap; null until fetched, so only server validation applies.</summary>
  public int? ImageLimit { get => imageLimit; private set => SetProperty(ref imageLimit, value); }

  private bool HasImageCapacity => ImageLimit is not { } limit || Images.Count < limit;

  public async Task LoadImageLimitAsync(CancellationToken cancellationToken = default)
  {
    var generation = Interlocked.Increment(ref imageLimitGeneration);
    int? limit;
    try
    {
      limit = await postsService.FetchPostImageLimitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (HttpRequestException) { limit = null; }
    if (generation != Volatile.Read(ref imageLimitGeneration)) return;
    ImageLimit = limit;
    OnPropertyChanged(nameof(CanAddImages));
    OnPropertyChanged(nameof(CanUploadMoreImagesInBatch));
    OnPropertyChanged(nameof(ImageCountLabel));
  }

  private void ReportImageLimitReached()
  {
    if (ImageLimit is { } limit)
    {
      CompleteError(localization.Format(UiMessageKey.SharedPostImageLimitMax, ("max", limit)));
    }
  }
}
