using System.Diagnostics.CodeAnalysis;
using Voucha.Client.App.Support;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage
{
  private async void OnUploadLogoClicked(object? sender, EventArgs e) =>
      await PickAndUploadImageAsync(isLogo: true).ConfigureAwait(true);

  private async void OnUploadHeroClicked(object? sender, EventArgs e) =>
      await PickAndUploadImageAsync(isLogo: false).ConfigureAwait(true);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "File picker failures are displayed inline.")]
  private async Task PickAndUploadImageAsync(bool isLogo)
  {
    var generation = isLogo ? logoPreviewGeneration : heroPreviewGeneration;
    bool IsCurrent() => generation == (isLogo ? logoPreviewGeneration : heroPreviewGeneration);
    try
    {
      var file = await FilePicker.Default.PickAsync(new PickOptions
      {
        PickerTitle = UiCopy.Localize(UiMessageKey.NativeDotnetCsharpEditorSelectImage),
      }).ConfigureAwait(true);
      if (file is null || !IsCurrent()) return;
      await using var selection = await ImageSelectionLoader.LoadAsync(file).ConfigureAwait(true);
      if (!IsCurrent()) return;
      await UploadSelectedImageAsync(isLogo, selection.Content, selection.ContentType).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      if (IsCurrent()) StatusLabel.Text = ex.Message;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Upload failures are displayed inline.")]
  internal async Task UploadSelectedImageAsync(bool isLogo, Stream selectedBytes, string contentType)
  {
    var target = isLogo ? LogoImageEntry : HeroImageEntry;
    var localPreview = isLogo ? LogoLocalPreviewImage : HeroLocalPreviewImage;
    var unavailableLabel = isLogo ? LogoPreviewUnavailableLabel : HeroPreviewUnavailableLabel;
    var generation = isLogo ? ++logoPreviewGeneration : ++heroPreviewGeneration;
    bool IsCurrent() => generation == (isLogo ? logoPreviewGeneration : heroPreviewGeneration);
    ClearLocalPreview(localPreview);
    unavailableLabel.IsVisible = false;
    if (isLogo) logoLocalImageId = null;
    else heroLocalImageId = null;
    try
    {
      var preview = await LocalImagePreview.ReadSelectedBytesAsync(selectedBytes).ConfigureAwait(true);
      if (!IsCurrent()) return;
      localPreview.Source = preview.CanPreview ? LocalImagePreview.SourceFromBytes(preview.Bytes) : null;
      localPreview.IsVisible = preview.CanPreview;
      using var stream = new MemoryStream(preview.Bytes, writable: false);
      var upload = await imageUploadService
          .CreateUploadUrlAsync(new CreateImageUploadUrlBody(contentType, preview.Bytes.Length))
          .ConfigureAwait(true);
      if (!IsCurrent()) return;
      await imageUploadService.UploadAsync(upload.Upload, stream, stream.Length).ConfigureAwait(true);
      if (!IsCurrent()) return;
      var completed = await imageUploadService.CompleteAsync(upload.Upload.ImageId).ConfigureAwait(true);
      if (!IsCurrent()) return;
      if (isLogo) logoLocalImageId = completed.Image.Id;
      else heroLocalImageId = completed.Image.Id;
      target.Text = completed.Image.Id;
      unavailableLabel.IsVisible = !preview.CanPreview;
    }
    catch (Exception ex)
    {
      if (!IsCurrent()) return;
      ClearLocalPreview(localPreview);
      unavailableLabel.IsVisible = false;
      StatusLabel.Text = ex.Message;
    }
  }
}
