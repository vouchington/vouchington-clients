using System.Diagnostics.CodeAnalysis;
using Voucha.Client.App.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class PostComposePage
{
  private void OnRemovePendingPreviewClicked(object? sender, EventArgs e)
  {
    imageSelectionBatchCancellationSource?.Cancel();
    viewModel.CancelPendingImagePreview();
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not let picker or upload failures escape.")]
  private async void OnAddImageClicked(object? sender, EventArgs e)
  {
    CancellationTokenSource? batchCancellationSource = null;
    try
    {
      if (!viewModel.CanAddImages) return;

      batchCancellationSource = new CancellationTokenSource();
      var previousBatchCancellationSource = Interlocked.Exchange(
          ref imageSelectionBatchCancellationSource,
          batchCancellationSource);
      previousBatchCancellationSource?.Cancel();

      var results = await ImageSelectionLoader.PickImagesAsync();
      if (results is null || results.Count == 0) return;
      if (batchCancellationSource.IsCancellationRequested) return;

      using var batch = viewModel.BeginImageUploadBatch();
      foreach (var result in results)
      {
        if (!viewModel.CanUploadMoreImagesInBatch) break;
        if (batchCancellationSource.IsCancellationRequested) break;

        try
        {
          await using var selection = await ImageSelectionLoader.LoadAsync(result, batchCancellationSource.Token);
          if (batchCancellationSource.IsCancellationRequested) break;
          if (!viewModel.CanUploadMoreImagesInBatch) break;

          var preview = await LocalImagePreview.ReadSelectedBytesAsync(
              selection.Content, batchCancellationSource.Token);
          var retainedThumbnail = preview.CanPreview
              ? LocalImagePreview.RetainedThumbnailFromBytes(preview.Bytes)
              : null;
          using var uploadContent = new MemoryStream(preview.Bytes, writable: false);
          var uploaded = await viewModel.UploadImageWithPreviewAsync(
              uploadContent,
              selection.ContentType,
              preview.Bytes.Length,
              preview.CanPreview ? preview.Bytes : null,
              !preview.CanPreview,
              cancellationToken: batchCancellationSource.Token,
              completedPreviewBytes: retainedThumbnail);
          if (!uploaded) break;
        }
        catch (Exception ex)
        {
          System.Diagnostics.Debug.WriteLine(ex);
          viewModel.ReportImageUploadFailure(
              ex is InvalidOperationException
                  ? ex.Message
                  : UiCopy.Localize(UiMessageKey.NativeDotnetCsharpImageUploadFailed));
          break;
        }
      }
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
      viewModel.ReportImageUploadFailure(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpImageUploadFailed));
    }
    finally
    {
      if (ReferenceEquals(imageSelectionBatchCancellationSource, batchCancellationSource))
      {
        imageSelectionBatchCancellationSource = null;
      }

      batchCancellationSource?.Dispose();
    }
  }

}
