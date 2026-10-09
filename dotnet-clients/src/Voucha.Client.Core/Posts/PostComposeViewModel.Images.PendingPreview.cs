namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  public ReadOnlyMemory<byte>? PendingLocalPreviewBytes { get; private set; }

  public bool HasPendingLocalPreview => PendingLocalPreviewBytes is { Length: > 0 };

  public bool PendingPreviewUnavailable { get; private set; }

  public bool HasPendingImagePreview => HasPendingLocalPreview || PendingPreviewUnavailable;

  private void SetPendingLocalPreview(ReadOnlyMemory<byte>? bytes, bool unavailable)
  {
    PendingLocalPreviewBytes = bytes;
    PendingPreviewUnavailable = unavailable;
    OnPropertyChanged(nameof(PendingLocalPreviewBytes));
    OnPropertyChanged(nameof(HasPendingLocalPreview));
    OnPropertyChanged(nameof(PendingPreviewUnavailable));
    OnPropertyChanged(nameof(HasPendingImagePreview));
  }

  public void CancelPendingImagePreview()
  {
    Interlocked.Increment(ref imageDraftGeneration);
    SetPendingLocalPreview(null, false);
    SetImages(Images.Where(image => !image.IsUploading).ToArray());
  }
}
