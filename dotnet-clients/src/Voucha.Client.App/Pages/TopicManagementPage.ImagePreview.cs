using System.Diagnostics.CodeAnalysis;
using Voucha.Client.App.Support;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage
{
  private const int UploadPollAttempts = 30;
  private static readonly TimeSpan UploadPollInterval = TimeSpan.FromSeconds(2);

  private async void OnUploadLogoClicked(object? sender, EventArgs e) =>
      await PickAndUploadImageAsync(isLogo: true).ConfigureAwait(true);

  private async void OnUploadHeroClicked(object? sender, EventArgs e) =>
      await PickAndUploadImageAsync(isLogo: false).ConfigureAwait(true);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "File picker failures are displayed inline.")]
  private async Task PickAndUploadImageAsync(bool isLogo)
  {
    var operation = BeginImageUpload(isLogo);
    if (operation is null) return;
    var active = operation.Value;
    bool IsCurrent() => !active.Cancellation.IsCancellationRequested &&
        active.Generation == (isLogo ? logoPreviewGeneration : heroPreviewGeneration);
    try
    {
      var file = await FilePicker.Default.PickAsync(new PickOptions
      {
        PickerTitle = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpEditorSelectImage),
      }).ConfigureAwait(true);
      if (file is null || !IsCurrent()) return;
      await using var selection = await ImageSelectionLoader.LoadAsync(file, active.Cancellation.Token).ConfigureAwait(true);
      await UploadSelectedImageCoreAsync(isLogo, selection.Content, selection.ContentType,
          active.Generation, active.Cancellation.Token).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      if (IsCurrent()) StatusLabel.Text = ex.Message;
    }
    finally
    {
      EndImageUpload(isLogo, active.Cancellation);
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Upload failures are displayed inline.")]
  internal async Task UploadSelectedImageAsync(bool isLogo, Stream selectedBytes, string contentType)
  {
    var operation = BeginImageUpload(isLogo);
    if (operation is null) return;
    var active = operation.Value;
    try
    {
      await UploadSelectedImageCoreAsync(isLogo, selectedBytes, contentType,
          active.Generation, active.Cancellation.Token).ConfigureAwait(true);
    }
    finally
    {
      EndImageUpload(isLogo, active.Cancellation);
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Upload failures are displayed inline.")]
  private async Task UploadSelectedImageCoreAsync(
      bool isLogo, Stream selectedBytes, string contentType, int generation, CancellationToken cancellationToken)
  {
    var target = isLogo ? LogoImageEntry : HeroImageEntry;
    var localPreview = isLogo ? LogoLocalPreviewImage : HeroLocalPreviewImage;
    var unavailableLabel = isLogo ? LogoPreviewUnavailableLabel : HeroPreviewUnavailableLabel;
    bool IsCurrent() => !cancellationToken.IsCancellationRequested &&
        generation == (isLogo ? logoPreviewGeneration : heroPreviewGeneration);
    ClearLocalPreview(localPreview);
    unavailableLabel.IsVisible = false;
    if (isLogo) logoLocalImageId = null;
    else heroLocalImageId = null;
    try
    {
      var preview = await LocalImagePreview.ReadSelectedBytesAsync(selectedBytes, cancellationToken).ConfigureAwait(true);
      if (!IsCurrent()) return;
      localPreview.Source = preview.PreviewBytes is { } selectedThumbnail
          ? LocalImagePreview.SourceFromBytes(selectedThumbnail) : null;
      localPreview.IsVisible = preview.CanPreview;
      using var stream = new MemoryStream(preview.Bytes, writable: false);
      var upload = await imageUploadService
          .CreateUploadUrlAsync(new CreateImageUploadUrlBody(contentType, preview.Bytes.Length), cancellationToken)
          .ConfigureAwait(true);
      if (!IsCurrent()) return;
      await imageUploadService.UploadAsync(upload.Upload, stream, stream.Length, cancellationToken).ConfigureAwait(true);
      if (!IsCurrent()) return;
      var completed = await imageUploadService.CompleteAsync(upload.Upload.ImageId, cancellationToken).ConfigureAwait(true);
      if (!IsCurrent()) return;
      var ready = await WaitForReadyAsync(completed.Image.Id, cancellationToken).ConfigureAwait(true);
      if (!IsCurrent()) return;
      if (isLogo) logoLocalImageId = ready.Id;
      else heroLocalImageId = ready.Id;
      target.Text = ready.Id;
      var thumbnail = preview.PreviewBytes;
      localPreview.Source = thumbnail is null ? null : LocalImagePreview.SourceFromBytes(thumbnail);
      localPreview.IsVisible = thumbnail is not null;
      unavailableLabel.IsVisible = thumbnail is null;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
      if (!IsCurrent()) return;
      ClearLocalPreview(localPreview);
      unavailableLabel.IsVisible = false;
      StatusLabel.Text = ex.Message;
    }
  }

  private async Task<ImageUploadState> WaitForReadyAsync(string imageId, CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < UploadPollAttempts; attempt++)
    {
      try
      {
        var state = (await imageUploadService.FetchUploadStateAsync(imageId, cancellationToken)
            .ConfigureAwait(true)).UploadState;
        cancellationToken.ThrowIfCancellationRequested();
        if (state.Blocked || state.UploadStatus is "failed" or "error")
          throw new InvalidOperationException(state.UploadError ?? UiCopy.Localize(UiMessageKey.NativeDotnetCsharpImageUploadFailed));
        if (state.Ready) return state;
      }
      catch (Exception ex) when (ImageUploadPolling.IsTransientFailure(ex) && attempt + 1 < UploadPollAttempts)
      {
      }
      if (attempt + 1 < UploadPollAttempts)
        await Task.Delay(UploadPollInterval, cancellationToken).ConfigureAwait(true);
    }
    throw new TimeoutException(UiCopy.Localize(UiMessageKey.NativeDotnetCsharpImageUploadTimedOut));
  }

  private (int Generation, CancellationTokenSource Cancellation)? BeginImageUpload(bool isLogo)
  {
    if (isSaving || (isLogo ? logoUploadCancellation : heroUploadCancellation) is not null) return null;
    var cancellation = new CancellationTokenSource();
    if (isLogo) logoUploadCancellation = cancellation;
    else heroUploadCancellation = cancellation;
    UpdateImageActionState();
    return (isLogo ? ++logoPreviewGeneration : ++heroPreviewGeneration, cancellation);
  }

  private void CancelImageUpload(bool isLogo) =>
      (isLogo ? logoUploadCancellation : heroUploadCancellation)?.Cancel();

  private void EndImageUpload(bool isLogo, CancellationTokenSource cancellation)
  {
    if (isLogo && ReferenceEquals(logoUploadCancellation, cancellation)) logoUploadCancellation = null;
    if (!isLogo && ReferenceEquals(heroUploadCancellation, cancellation)) heroUploadCancellation = null;
    UpdateImageActionState();
    cancellation.Dispose();
  }

  private void UpdateImageActionState()
  {
    SaveButton.IsEnabled = !isSaving && logoUploadCancellation is null && heroUploadCancellation is null;
    UploadLogoButton.IsEnabled = !isSaving && logoUploadCancellation is null;
    UploadHeroButton.IsEnabled = !isSaving && heroUploadCancellation is null;
  }
}
