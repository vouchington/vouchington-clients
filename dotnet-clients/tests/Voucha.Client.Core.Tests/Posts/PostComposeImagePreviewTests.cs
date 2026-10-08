using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class PostComposeImageTests
{
  [Fact]
  public async Task SelectedBytesAppearBeforeHeldUploadUrlAndCannotBecomePublishableAfterRemoval()
  {
    var service = new RecordingImageUploadService { BlockCreateUploadUrl = true };
    var viewModel = NewViewModel(imageUploadService: service);
    using var content = new MemoryStream([1, 2, 3]);
    var upload = viewModel.UploadImageWithPreviewAsync(
        content, "image/png", content.Length, new byte[] { 1, 2, 3 }, false,
        cancellationToken: TestContext.Current.CancellationToken);
    try
    {
      await service.CreateUploadUrlStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.True(viewModel.HasPendingLocalPreview);
      Assert.Equal(new byte[] { 1, 2, 3 }, viewModel.PendingLocalPreviewBytes?.ToArray());
      Assert.False(viewModel.PendingPreviewUnavailable);
      Assert.Empty(viewModel.Images);
      Assert.False(viewModel.Validation.CanPublish);
      viewModel.CancelPendingImagePreview();
      Assert.False(viewModel.HasPendingImagePreview);
    }
    finally
    {
      service.ReleaseCreateUploadUrl.TrySetResult(true);
      await upload;
    }
    Assert.False(await upload);
    Assert.Empty(viewModel.Images);
  }

  [Fact]
  public async Task FailedHeldUploadUrlClearsUnavailablePreviewWithoutLosingUploadError()
  {
    var service = new RecordingImageUploadService
    {
      BlockCreateUploadUrl = true,
      FailCreateUploadUrl = true,
    };
    var viewModel = NewViewModel(imageUploadService: service);
    using var content = new MemoryStream([1, 2, 3]);
    var upload = viewModel.UploadImageWithPreviewAsync(
        content, "image/heic", content.Length, null, true,
        cancellationToken: TestContext.Current.CancellationToken);
    try
    {
      await service.CreateUploadUrlStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.True(viewModel.PendingPreviewUnavailable);
      Assert.True(viewModel.HasPendingImagePreview);
      Assert.False(viewModel.HasPendingLocalPreview);
      Assert.Empty(viewModel.Images);
    }
    finally
    {
      service.ReleaseCreateUploadUrl.TrySetResult(true);
      await upload;
    }
    Assert.False(await upload);
    Assert.False(viewModel.HasPendingImagePreview);
    Assert.Empty(viewModel.Images);
    Assert.NotNull(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task PreviewDecodeFailureKeepsUploadedImageInPublishRequest()
  {
    var posts = new RecordingPostsService();
    var viewModel = NewViewModel(posts, new RecordingImageUploadService());
    using var content = new MemoryStream([1, 2, 3]);

    Assert.True(await viewModel.UploadImageWithPreviewAsync(
        content, "image/heic", content.Length, null, true,
        cancellationToken: TestContext.Current.CancellationToken));

    var image = Assert.Single(viewModel.Images);
    Assert.Equal("image-1", image.ImageId);
    Assert.True(image.PreviewUnavailable);
    Assert.False(image.HasLocalPreview);
    Assert.True(image.HasUploadedPreviewUnavailable);

    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "token";
    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal("image-1", Assert.Single(posts.Body?.Images ?? []).ImageId);
  }

  [Fact]
  public async Task PreviewUnavailableSuccessCopyWaitsForHeldUploadCompletion()
  {
    var service = new RecordingImageUploadService { BlockUpload = true };
    var viewModel = NewViewModel(imageUploadService: service);
    using var content = new MemoryStream([1, 2, 3]);
    var upload = viewModel.UploadImageWithPreviewAsync(
        content, "image/heic", content.Length, null, true,
        cancellationToken: TestContext.Current.CancellationToken);
    try
    {
      await service.UploadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      var pending = Assert.Single(viewModel.Images);
      Assert.True(pending.PreviewUnavailable);
      Assert.False(pending.HasUploadedPreviewUnavailable);
    }
    finally
    {
      service.ReleaseUpload.TrySetResult(true);
      await upload;
    }
    Assert.True(await upload);
    Assert.True(Assert.Single(viewModel.Images).HasUploadedPreviewUnavailable);
  }

  [Fact]
  public async Task LeavingPageDiscardsPendingPreviewAndIgnoresLateUpload()
  {
    var service = new RecordingImageUploadService { BlockUpload = true };
    var viewModel = NewViewModel(imageUploadService: service);
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft(
        "ready", 0, LocalPreviewBytes: new byte[] { 4, 5, 6 })));
    using var content = new MemoryStream([1, 2, 3]);
    var upload = viewModel.UploadImageWithPreviewAsync(
        content, "image/png", content.Length, new byte[] { 1, 2, 3 }, false,
        cancellationToken: TestContext.Current.CancellationToken);
    try
    {
      await service.UploadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.Equal(2, viewModel.Images.Count);
      viewModel.EndLocalImagePreviewSession();
      var retained = Assert.Single(viewModel.Images);
      Assert.Equal("ready", retained.ImageId);
      Assert.False(retained.HasLocalPreview);
    }
    finally
    {
      service.ReleaseUpload.TrySetResult(true);
      await upload;
    }
    Assert.False(await upload);
    Assert.Equal("ready", Assert.Single(viewModel.Images).ImageId);
  }
}
