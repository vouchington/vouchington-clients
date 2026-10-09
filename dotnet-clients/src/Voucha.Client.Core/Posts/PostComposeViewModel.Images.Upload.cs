using System.Threading;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private const long MaxImageUploadBytes = 50L * 1024 * 1024;
  private int imageUploadInProgress;

  public void ReportImageUploadFailure(string message) => CompleteError(message);

  public Task<bool> UploadImageAsync(
      Stream content,
      string contentType,
      long contentLength,
      string? caption = null,
      CancellationToken cancellationToken = default) =>
      UploadImageWithPreviewAsync(content, contentType, contentLength, null, false, caption, null, cancellationToken);

  public async Task<bool> UploadImageWithPreviewAsync(
      Stream content,
      string contentType,
      long contentLength,
      ReadOnlyMemory<byte>? localPreviewBytes,
      bool previewUnavailable,
      string? caption = null,
      ReadOnlyMemory<byte>? completedPreviewBytes = null,
      CancellationToken cancellationToken = default)
  {
    if (Interlocked.CompareExchange(ref imageUploadInProgress, 1, 0) != 0)
    {
      return false;
    }

    OnImageUploadInProgressChanged();
    try
    {
      var imageDraftGeneration = ImageDraftGeneration;
      if (imageUploadService is null)
      {
        CompleteError(localization.Localize(UiMessageKey.NativeDotnetCsharpImageUploadsUnavailable));
        return false;
      }
      if (Images.Count >= MaxImages) return false;
      if (contentLength is < 0 or > MaxImageUploadBytes)
      {
        CompleteError(localization.Localize(UiMessageKey.NativeDotnetCsharpImageTooLarge));
        return false;
      }

      string? uploadedImageId = null;
      try
      {
        ClearImageUploadError();
        SetPendingLocalPreview(localPreviewBytes, previewUnavailable);
        var upload = await imageUploadService.CreateUploadUrlAsync(
            new CreateImageUploadUrlBody(contentType, checked((int)contentLength)),
            cancellationToken).ConfigureAwait(true);
        if (!IsCurrentImageDraftGeneration(imageDraftGeneration)) return false;
        uploadedImageId = upload.Upload.ImageId;
        if (!TryAddImageDraft(new PostComposeImageDraft(
                uploadedImageId,
                Images.Count,
                EmptyToNull(caption),
                true,
                0d,
                LocalPreviewBytes: localPreviewBytes,
                PreviewUnavailable: previewUnavailable)))
        {
          return false;
        }
        SetPendingLocalPreview(null, false);

        await imageUploadService.UploadAsync(upload.Upload, content, contentLength, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentImageDraftGeneration(imageDraftGeneration)) return false;
        CompleteImageUploadResponse completion;
        for (var attempt = 0; ; attempt++)
        {
          if (!IsCurrentImageDraftGeneration(imageDraftGeneration)) return false;
          try
          {
            completion = await imageUploadService.CompleteAsync(upload.Upload.ImageId, cancellationToken).ConfigureAwait(true);
            break;
          }
          catch (Exception ex) when (ImageUploadPolling.IsTransientFailure(ex) && attempt + 1 < imageUploadPollAttempts)
          {
            await DelayForImageUploadPollAsync(cancellationToken).ConfigureAwait(true);
          }
        }

        if (!IsCurrentImageDraftGeneration(imageDraftGeneration)) return false;
        var completedImageId = completion.Image.Id;
        if (!string.Equals(completedImageId, uploadedImageId, StringComparison.Ordinal))
        {
          if (Images.Any(image => string.Equals(image.ImageId, completedImageId, StringComparison.Ordinal)))
          {
            var existingState = await WaitForImageReadyAsync(completedImageId, imageDraftGeneration, cancellationToken).ConfigureAwait(true);
            if (!IsCurrentImageDraftGeneration(imageDraftGeneration)) return false;
            RemoveImage(uploadedImageId);
            if (!IsAttachableImageUploadState(existingState))
            {
              UpdateImage(completedImageId, image => image with
              {
                IsUploading = false,
                UploadProgress = 0d,
                UploadError = existingState.Blocked
                    ? localization.Localize(UiMessageKey.NativeDotnetCsharpImageBlocked)
                    : existingState.UploadError ?? localization.Localize(UiMessageKey.NativeDotnetCsharpImageUploadFailed),
              });
              return false;
            }

            UpdateImage(completedImageId, image => image with { IsUploading = false, UploadProgress = 1d, UploadError = null });
            ClearImageUploadError();
            return true;
          }

          if (!IsCurrentImageDraftGeneration(imageDraftGeneration)) return false;
          UpdateImage(uploadedImageId, image => image with { ImageId = completedImageId });
          uploadedImageId = completedImageId;
        }

        var state = await WaitForImageReadyAsync(uploadedImageId, imageDraftGeneration, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentImageDraftGeneration(imageDraftGeneration)) return false;
        if (!IsAttachableImageUploadState(state))
        {
          UpdateImage(uploadedImageId, image => image with
          {
            IsUploading = false,
            LocalPreviewBytes = null,
            UploadProgress = 0d,
            UploadError = state.Blocked
                ? localization.Localize(UiMessageKey.NativeDotnetCsharpImageBlocked)
                : state.UploadError ?? localization.Localize(UiMessageKey.NativeDotnetCsharpImageUploadFailed),
          });
          return false;
        }

        UpdateImage(uploadedImageId, image => CompleteImagePreview(
            image, localPreviewBytes, completedPreviewBytes, previewUnavailable));
        ClearImageUploadError();
        return true;
      }
      catch (OperationCanceledException)
      {
        if (uploadedImageId is not null)
        {
          UpdateImage(uploadedImageId, image => image with
          {
            IsUploading = false,
            LocalPreviewBytes = null,
            UploadProgress = 0d,
            UploadError = localization.Localize(UiMessageKey.NativeDotnetCsharpImageUploadCancelled),
          });
        }
        return false;
      }
      catch (TimeoutException ex)
      {
        if (uploadedImageId is not null)
        {
          UpdateImage(uploadedImageId, image => image with { IsUploading = false, UploadProgress = 0d, UploadError = ex.Message, LocalPreviewBytes = null });
        }
        else
        {
          CompleteError(ex.Message);
        }
        return false;
      }
      catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
      {
        if (uploadedImageId is not null)
        {
          UpdateImage(uploadedImageId, image => image with { IsUploading = false, UploadProgress = 0d, UploadError = ex.Message, LocalPreviewBytes = null });
        }
        else
        {
          CompleteError(localization.Localize(UiMessageKey.NativeDotnetCsharpImageUploadFailed));
        }
        return false;
      }
    }
    finally
    {
      SetPendingLocalPreview(null, false);
      Interlocked.Exchange(ref imageUploadInProgress, 0);
      OnImageUploadInProgressChanged();
    }
  }


}
