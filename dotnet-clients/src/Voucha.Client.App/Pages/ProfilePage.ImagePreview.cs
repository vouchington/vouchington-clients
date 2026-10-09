using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Storage;
using Voucha.Client.App.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private int avatarPreviewGeneration;
  private readonly AvatarPreviewCancellation avatarPreviewCancellation = new();

  private void ClearLocalAvatarPreview()
  {
    AvatarLocalPreviewImage.Source = null;
    AvatarLocalPreviewImage.IsVisible = false;
    AvatarPreviewUnavailableLabel.IsVisible = false;
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not let picker or upload failures escape.")]
  private async void OnUploadAvatarClicked(object? sender, EventArgs e)
  {
    if (!viewModel.CanMutateAvatar) return;
    var generation = ++avatarPreviewGeneration;
    using var cancellation = new CancellationTokenSource();
    avatarPreviewCancellation.Replace(cancellation);
    ClearLocalAvatarPreview();
    try
    {
      var result = await FilePicker.PickAsync(PickOptions.Images);
      if (result is null) return;
      if (generation != avatarPreviewGeneration) return;

      await using var selection = await ImageSelectionLoader.LoadAsync(result, cancellation.Token);
      var preview = await LocalImagePreview.ReadSelectedBytesAsync(selection.Content, cancellation.Token);
      if (generation != avatarPreviewGeneration) return;
      AvatarLocalPreviewImage.Source = preview.PreviewBytes is { } thumbnail
          ? LocalImagePreview.SourceFromBytes(thumbnail) : null;
      AvatarLocalPreviewImage.IsVisible = preview.CanPreview;
      using var uploadContent = new MemoryStream(preview.Bytes, writable: false);
      var uploaded = await viewModel.UploadAvatarAsync(
          uploadContent,
          selection.ContentType,
          preview.Bytes.Length,
          cancellation.Token);
      if (generation == avatarPreviewGeneration)
      {
        AvatarPreviewUnavailableLabel.IsVisible = uploaded && !preview.CanPreview;
      }
    }
    catch (Exception ex)
    {
      if (generation != avatarPreviewGeneration) return;
      System.Diagnostics.Debug.WriteLine(ex);
      viewModel.ReportAvatarUploadFailure(
          ex is InvalidOperationException
              ? ex.Message
              : UiCopy.Localize(UiMessageKey.NativeDotnetCsharpAvatarUploadFailed));
    }
    finally
    {
      avatarPreviewCancellation.Complete(cancellation);
      if (generation == avatarPreviewGeneration)
      {
        AvatarLocalPreviewImage.Source = null;
        AvatarLocalPreviewImage.IsVisible = false;
      }
    }
  }

  private async void OnRemoveAvatarClicked(object? sender, EventArgs e)
  {
    if (!viewModel.CanMutateAvatar) return;
    avatarPreviewGeneration++;
    avatarPreviewCancellation.CancelCurrent();
    ClearLocalAvatarPreview();
    await viewModel.RemoveAvatarAsync();
  }

}
