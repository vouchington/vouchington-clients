using System.Runtime.InteropServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class PostComposeImageTests
{
  [Fact]
  public async Task SuccessfulUploadRetainsOnlyIndependentBoundedThumbnail()
  {
    var service = new RecordingImageUploadService { BlockUpload = true };
    var viewModel = NewViewModel(imageUploadService: service);
    var selected = new byte[512 * 1024];
    var thumbnail = new byte[128];
    using var content = new MemoryStream(selected);
    var upload = viewModel.UploadImageWithPreviewAsync(
        content, "image/png", content.Length, selected, false,
        cancellationToken: TestContext.Current.CancellationToken,
        completedPreviewBytes: thumbnail);
    try
    {
      await service.UploadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.Equal(selected.Length, Assert.Single(viewModel.Images).LocalPreviewBytes?.Length);
    }
    finally
    {
      service.ReleaseUpload.TrySetResult(true);
      await upload;
    }

    Assert.True(await upload);
    var ready = Assert.Single(viewModel.Images);
    Assert.Equal("image-1", ready.ImageId);
    Assert.True(ready.IsReady);
    Assert.Equal(thumbnail, ready.LocalPreviewBytes?.ToArray());
    Assert.False(ready.HasUploadedPreviewUnavailable);
    Assert.True(MemoryMarshal.TryGetArray(ready.LocalPreviewBytes!.Value, out var retained));
    Assert.NotSame(selected, retained.Array);
    Assert.NotSame(thumbnail, retained.Array);
  }

  [Fact]
  public async Task OversizedThumbnailFallsBackWithoutLosingReadyImage()
  {
    var viewModel = NewViewModel(imageUploadService: new RecordingImageUploadService());
    var selected = new byte[128 * 1024];
    using var content = new MemoryStream(selected);

    Assert.True(await viewModel.UploadImageWithPreviewAsync(
        content, "image/png", content.Length, selected, false,
        cancellationToken: TestContext.Current.CancellationToken,
        completedPreviewBytes: new byte[256 * 1024 + 1]));

    var ready = Assert.Single(viewModel.Images);
    Assert.Equal("image-1", ready.ImageId);
    Assert.False(ready.HasLocalPreview);
    Assert.True(ready.HasUploadedPreviewUnavailable,
        $"ready={ready.IsReady} uploading={ready.IsUploading} previewUnavailable={ready.PreviewUnavailable} retained={ready.LocalPreviewBytes?.Length}");
  }

  [Fact]
  public async Task TwentyCompletedUploadsRetainOnlySmallIndependentPreviews()
  {
    var viewModel = NewViewModel(imageUploadService: new SequentialImageUploadService());
    for (var index = 0; index < 20; index++)
    {
      var selected = new byte[64 * 1024];
      var thumbnail = new byte[64];
      using var content = new MemoryStream(selected);
      Assert.True(await viewModel.UploadImageWithPreviewAsync(
          content, "image/png", content.Length, selected, false,
          cancellationToken: TestContext.Current.CancellationToken,
          completedPreviewBytes: thumbnail));
    }

    Assert.Equal(20, viewModel.Images.Count);
    Assert.All(viewModel.Images, image =>
    {
      Assert.True(image.IsReady);
      Assert.Equal(64, image.LocalPreviewBytes?.Length);
    });
    Assert.Equal(20 * 64, viewModel.Images.Sum(image => image.LocalPreviewBytes?.Length ?? 0));
  }

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
    Assert.True(image.PreviewUnavailable,
        $"ready={image.IsReady} uploading={image.IsUploading} retained={image.LocalPreviewBytes?.Length}");
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
    var ready = Assert.Single(viewModel.Images);
    Assert.True(ready.HasUploadedPreviewUnavailable,
        $"ready={ready.IsReady} uploading={ready.IsUploading} previewUnavailable={ready.PreviewUnavailable} retained={ready.LocalPreviewBytes?.Length}");
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

  private sealed class SequentialImageUploadService : IImageUploadService
  {
    private int nextImageId;

    public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
        CreateImageUploadUrlBody body, CancellationToken cancellationToken = default)
    {
      var imageId = $"image-{++nextImageId}";
      return Task.FromResult(new ImageUploadUrlResponse(new ImageUploadUrl(
          imageId, $"https://upload.example.test/{imageId}", body.ContentType, DateTimeOffset.UtcNow)));
    }

    public Task UploadAsync(ImageUploadUrl upload, Stream content, long contentLength,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<CompleteImageUploadResponse> CompleteAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CompleteImageUploadResponse(new ImageUpload(imageId, "complete")));

    public Task<ImageUploadStateResponse> FetchUploadStateAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImageUploadStateResponse(new ImageUploadState(
            imageId, "complete", null, true, false)));
  }
}
