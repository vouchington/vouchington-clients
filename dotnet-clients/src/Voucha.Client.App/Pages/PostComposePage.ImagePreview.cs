using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Storage;
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

  private async void OnAddImageClicked(object? sender, EventArgs e)
      => await AddImagesAsync();

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "The picker and upload helper converts recoverable failures to visible page state.")]
  internal async Task AddImagesAsync(
      Func<Task<IReadOnlyList<FileResult>?>>? pickImages = null,
      Func<FileResult, CancellationToken, Task<ImageSelection>>? loadImage = null)
  {
    pickImages ??= ImageSelectionLoader.PickImagesAsync;
    loadImage ??= ImageSelectionLoader.LoadAsync;
    CancellationTokenSource? batchCancellationSource = null;
    try
    {
      if (!viewModel.CanAddImages) return;

      batchCancellationSource = new CancellationTokenSource();
      var previousBatchCancellationSource = Interlocked.Exchange(
          ref imageSelectionBatchCancellationSource,
          batchCancellationSource);
      previousBatchCancellationSource?.Cancel();

      var results = await pickImages();
      if (results is null || results.Count == 0) return;
      if (batchCancellationSource.IsCancellationRequested) return;

      using var batch = viewModel.BeginImageUploadBatch();
      foreach (var result in results)
      {
        if (!viewModel.CanUploadMoreImagesInBatch) break;
        if (batchCancellationSource.IsCancellationRequested) break;

        try
        {
          await using var selection = await loadImage(result, batchCancellationSource.Token);
          if (batchCancellationSource.IsCancellationRequested) break;
          if (!viewModel.CanUploadMoreImagesInBatch) break;

          var preview = await LocalImagePreview.ReadSelectedBytesAsync(
              selection.Content, batchCancellationSource.Token);
          var retainedThumbnail = preview.PreviewBytes;
          using var uploadContent = new MemoryStream(preview.Bytes, writable: false);
          var uploaded = await viewModel.UploadImageWithPreviewAsync(
              uploadContent,
              selection.ContentType,
              preview.Bytes.Length,
              preview.PreviewBytes,
              !preview.CanPreview,
              cancellationToken: batchCancellationSource.Token,
              completedPreviewBytes: retainedThumbnail);
          if (!uploaded) break;
        }
        catch (OperationCanceledException) when (batchCancellationSource.IsCancellationRequested)
        {
          break;
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
    catch (OperationCanceledException) when (batchCancellationSource?.IsCancellationRequested == true)
    {
      // Leaving the page or replacing the selection intentionally ends this batch.
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
