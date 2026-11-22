using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private int? imageLimit;

  /// Each load bumps this, so a response from an earlier load cannot overwrite a newer cap.
  private int imageLimitGeneration;

  /// Guards the generation check and the cap write as one step, so a load that finishes between
  /// a newer load's check and its write cannot publish a stale cap.
  private readonly Lock imageLimitLock = new();

  /// <summary>The server-configured image cap; null until fetched, so only server validation applies.</summary>
  public int? ImageLimit { get => imageLimit; private set => SetProperty(ref imageLimit, value); }

  private bool HasImageCapacity => ImageLimit is not { } limit || Images.Count < limit;

  public async Task LoadImageLimitAsync(CancellationToken cancellationToken = default)
  {
    int generation;
    lock (imageLimitLock) generation = ++imageLimitGeneration;
    int? limit;
    try
    {
      limit = await postsService.FetchPostImageLimitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (HttpRequestException) { limit = null; }
    lock (imageLimitLock)
    {
      if (generation != imageLimitGeneration) return;
      ImageLimit = limit;
    }
    OnPropertyChanged(nameof(CanAddImages));
    OnPropertyChanged(nameof(CanUploadMoreImagesInBatch));
    OnPropertyChanged(nameof(ImageCountLabel));
  }

  /// Admits an image after its upload URL was issued; reports the cap if a load lowered it meanwhile.
  private bool TryAdmitUploadedImage(PostComposeImageDraft image)
  {
    if (TryAddImageDraft(image)) return true;
    if (!HasImageCapacity) ReportImageLimitReached();
    return false;
  }

  private void ReportImageLimitReached()
  {
    if (ImageLimit is { } limit)
    {
      CompleteError(localization.Format(UiMessageKey.SharedPostImageLimitMax, ("max", limit)));
    }
  }
}
