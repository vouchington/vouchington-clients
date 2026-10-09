namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private const int MaxRetainedPreviewBytes = 256 * 1024;

  private static PostComposeImageDraft CompleteImagePreview(
      PostComposeImageDraft image,
      ReadOnlyMemory<byte>? selectedPreviewBytes,
      ReadOnlyMemory<byte>? completedPreviewBytes,
      bool previewUnavailable)
  {
    ReadOnlyMemory<byte>? retained = null;
    if (completedPreviewBytes is { Length: > 0 and <= MaxRetainedPreviewBytes })
    {
      retained = completedPreviewBytes.Value.ToArray();
    }
    return image with
    {
      IsUploading = false,
      UploadProgress = 1d,
      UploadError = null,
      LocalPreviewBytes = retained,
      PreviewUnavailable = retained is null && (previewUnavailable || selectedPreviewBytes is { Length: > 0 }),
    };
  }
}
